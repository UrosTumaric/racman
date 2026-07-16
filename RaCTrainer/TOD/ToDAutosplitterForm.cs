using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net.Sockets;
using System.Threading;

namespace racman.TOD
{
    public class ToDAutosplitterForm : TODForm
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
        private const string AutosplitterSprxName = "tod-autosplitter.sprx";

        private readonly object writerLock = new object();

        private MemoryMappedFile mmfFile;
        private MemoryMappedViewStream mmfStream;
        private BinaryWriter writer;

        private TcpClient autosplitterClient;
        private NetworkStream autosplitterStream;
        private Thread dataThread;
        private volatile bool stopping;

        public ToDAutosplitterForm(tod game)
            : base(game)
        {
        }

        protected override bool StartAutosplitterClient()
        {
            if (!(game.api is Ratchetron))
            {
                System.Windows.Forms.MessageBox.Show(
                    "SPRX autosplitter is not supported on RPCS3.",
                    "Autosplitter Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }

            if (dataThread != null)
                return true;

            func.PrepareSPRX(AttachPS3Form.ip, AutosplitterSprxName, 5);

            mmfFile = MemoryMappedFile.CreateOrOpen(AutosplitterMmfName, AutosplitterMmfSize);
            mmfStream = mmfFile.CreateViewStream();
            writer = new BinaryWriter(mmfStream);

            // command, paused, planet
            WriteAutosplitterValues(new byte[] { 0, 0, 0 });

            autosplitterClient = new TcpClient(AttachPS3Form.ip, AutosplitterPort);
            autosplitterClient.NoDelay = true;
            autosplitterStream = autosplitterClient.GetStream();

            stopping = false;
            dataThread = new Thread(ManageAutosplitterConnection);
            dataThread.IsBackground = true;
            dataThread.Name = "ToD Autosplitter";
            dataThread.Start();

            return true;
        }

        private void ManageAutosplitterConnection()
        {
            try
            {
                while (!stopping)
                {
                    int commandValue = autosplitterStream.ReadByte();
                    if (commandValue == -1)
                        break;

                    int packetValue = autosplitterStream.ReadByte();
                    if (packetValue == -1)
                        break;

                    // Confirm receipt after both protocol bytes have arrived.
                    autosplitterStream.WriteByte(1);

                    byte command = (byte)commandValue;
                    byte packet = (byte)packetValue;

                    Console.WriteLine("Got autosplitter command: {0} & packet: {1}", command, packet);

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
                    Console.WriteLine("ToD autosplitter connection closed.");
            }
            catch (SocketException)
            {
                if (!stopping)
                    Console.WriteLine("ToD autosplitter socket disconnected.");
            }
            catch (ObjectDisposedException)
            {
                // Expected when the form closes while the thread is reading.
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

        private void StopAutosplitterClient()
        {
            stopping = true;

            autosplitterStream?.Close();
            autosplitterClient?.Close();

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

            autosplitterStream = null;
            autosplitterClient = null;
            dataThread = null;
        }

        protected override void OnFormClosing(System.Windows.Forms.FormClosingEventArgs e)
        {
            StopAutosplitterClient();
            base.OnFormClosing(e);
        }
    }
}
