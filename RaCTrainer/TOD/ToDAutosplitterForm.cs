using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace racman.TOD
{
    public partial class ToDAutosplitterForm : Form
    {
        private enum SplitterCommand
        {
            Reset = 1,
            Split = 2,
            Pause = 3,
            Unpause = 4,
        }

        private const int AutosplitterPort = 9672;
        private const int AutosplitterMmfSize = 256;
        private const string AutosplitterMmfName = "racman-autosplitter-lc";

        private readonly object writerLock = new object();

        private MemoryMappedFile mmfFile;
        private MemoryMappedViewStream mmfStream;
        private BinaryWriter writer;

        private TcpClient client;
        private NetworkStream stream;
        private Thread dataThread;
        private volatile bool stopping;
        private int disconnectPopupShown;

        public ToDAutosplitterForm(string ip)
        {
            InitializeComponent();

            mmfFile = MemoryMappedFile.CreateOrOpen(
                AutosplitterMmfName,
                AutosplitterMmfSize);
            mmfStream = mmfFile.CreateViewStream();
            writer = new BinaryWriter(mmfStream);

            // command, paused, planet
            WriteAutosplitterValues(new byte[] { 0, 0, 0 });

            client = new TcpClient(ip, AutosplitterPort);
            client.NoDelay = true;
            stream = client.GetStream();

            dataThread = new Thread(ManageConnection);
            dataThread.IsBackground = true;
            dataThread.Name = "ToD Autosplitter";
            dataThread.Start();
        }

        private void ManageConnection()
        {
            bool disconnectedUnexpectedly = false;

            try
            {
                while (!stopping)
                {
                    int commandValue = stream.ReadByte();
                    if (commandValue == -1)
                    {
                        disconnectedUnexpectedly = true;
                        break;
                    }

                    int packetValue = stream.ReadByte();
                    if (packetValue == -1)
                    {
                        disconnectedUnexpectedly = true;
                        break;
                    }

                    // Confirm receipt after both protocol bytes have arrived.
                    stream.WriteByte(1);

                    byte command = (byte)commandValue;
                    byte packet = (byte)packetValue;

                    Console.WriteLine(
                        "Got autosplitter command: {0} & packet: {1}",
                        command,
                        packet);

                    switch ((SplitterCommand)command)
                    {
                        case SplitterCommand.Split:
                        case SplitterCommand.Reset:
                            WriteAutosplitterValue(2, packet);
                            PulseCommand(command);
                            break;

                        case SplitterCommand.Pause:
                            WriteAutosplitterValue(1, 1);
                            break;

                        case SplitterCommand.Unpause:
                            WriteAutosplitterValue(1, 0);
                            break;
                    }
                }
            }
            catch (IOException)
            {
                if (!stopping)
                {
                    Console.WriteLine("ToD autosplitter connection closed.");
                    disconnectedUnexpectedly = true;
                }
            }
            catch (SocketException)
            {
                if (!stopping)
                {
                    Console.WriteLine("ToD autosplitter socket disconnected.");
                    disconnectedUnexpectedly = true;
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected when the form closes while the thread is reading.
            }
            finally
            {
                if (disconnectedUnexpectedly && !stopping)
                    ShowDisconnectPopup();
            }
        }

        private void ShowDisconnectPopup()
        {
            if (Interlocked.Exchange(ref disconnectPopupShown, 1) != 0)
                return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (stopping || IsDisposed)
                        return;

                    labelStatus.Text = "Autosplitter disconnected.";
                    MessageBox.Show(
                        this,
                        "The connection to the PS3 autosplitter was lost.",
                        "Autosplitter Disconnected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }));
            }
            catch (InvalidOperationException)
            {
                // The form was disposed before the UI notification could run.
            }
        }

        private void PulseCommand(byte command)
        {
            WriteAutosplitterValue(0, command);
            Thread.Sleep(100);
            WriteAutosplitterValue(0, 0);
        }

        private void WriteAutosplitterValues(byte[] values)
        {
            lock (writerLock)
            {
                writer.Seek(0, SeekOrigin.Begin);
                writer.Write(values);
                writer.Flush();
            }
        }

        private void WriteAutosplitterValue(int offset, byte value)
        {
            lock (writerLock)
            {
                if (writer == null)
                    return;

                writer.Seek(offset, SeekOrigin.Begin);
                writer.Write(value);
                writer.Flush();
            }
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void StopAutosplitter()
        {
            stopping = true;

            stream?.Close();
            client?.Close();

            if (dataThread != null &&
                dataThread != Thread.CurrentThread &&
                dataThread.IsAlive)
            {
                dataThread.Join(500);
            }

            lock (writerLock)
            {
                writer?.Close();
                writer = null;

                mmfStream?.Close();
                mmfStream = null;

                mmfFile?.Dispose();
                mmfFile = null;
            }

            stream = null;
            client = null;
            dataThread = null;
        }

        private void ToDAutosplitterForm_FormClosing(
            object sender,
            FormClosingEventArgs e)
        {
            StopAutosplitter();
            Application.Exit();
        }
    }
}
