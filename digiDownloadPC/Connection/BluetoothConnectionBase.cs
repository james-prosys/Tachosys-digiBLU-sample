using System;
using System.Threading.Tasks;
using Tachosys.System;

namespace digiDownloadPC
{
    internal abstract class BluetoothConnectionBase
    {

        protected int insertPos;
        protected byte[] received;
        protected DateTime timeout;
        protected object commandLock = new object();

        public event DataEventHandler DataReceived;
        public event DataEventHandler DataSent;

        protected void OnDataReceived(DataEventArgs e)
        {
            DataEventHandler handler = DataReceived;
            if (handler != null) handler(this, e);
        }
        protected void OnDataSent(DataEventArgs e)
        {
            DataEventHandler handler = DataSent;
            if (handler != null) handler(this, e);
        }

        public BluetoothConnectionBase(string connectionPath)
        {
            this.ConnectionPath = connectionPath;
            this.RequestTimeout = DefaultRequestTimeout;
        }

        ~BluetoothConnectionBase()
        {
            Disconnect();
        }

        public abstract int DefaultRequestTimeout { get; }

        public int RequestTimeout { get; set; }

        public string ConnectionPath { get; private set; }

        public DateTime LastCommandTime { get; protected set; }

        public abstract Task<bool> Connect();

        public virtual void Disconnect()
        {
            LastCommandTime = DateTime.MinValue;
        }

        public virtual Task<byte[]> ExecuteVUCommand(byte[] command)
        {
            return ExecuteVUCommand(command, false);
        }

        public abstract Task<byte[]> ExecuteVUCommand(byte[] command, bool ignoreRequestOutOfRange);

        public abstract Task<byte[]> ExecuteCommand(byte[] data);

        public abstract Task Send(byte[] data);

        protected void ProcessReceived()
        {
            DataEventArgs args = new DataEventArgs((byte[])received.Clone());
            OnDataReceived(args);

            if (!Protocol.Packet.CheckChecksum(received)) {
                throw new ChecksumFailureException();
            }

            switch (received[Protocol.PacketBytePosition.Instruction] / 16) {
                case Protocol.Instructions.Groups.Acknowledge:
                    ReceiveAcknowledge();
                    break;
                default:
                    throw new CommandReplyNotRecognisedException();
            }
        }

        protected void ReceiveAcknowledge()
        {
            switch (received[Protocol.PacketBytePosition.Instruction]) {
                case Protocol.Instructions.Acknowledge.OK:
                    break;
                case Protocol.Instructions.Acknowledge.Error:
                    ReceiveError();
                    break;
                case Protocol.Instructions.Acknowledge.Failure:
                    throw new CommandNotRecognisedException();
                default:
                    throw new CommandReplyNotRecognisedException();
            }
            int length = ByteArray.ToInt16(received, Protocol.PacketBytePosition.Length);
            if (length > 0) {
                Array.Copy(received, Protocol.PacketBytePosition.Data, received, 0, length);
                Array.Resize(ref received, length);
            }
            else {
                received = new byte[] { };
            }
        }

        protected void ReceiveError()
        {
            byte[] errorCode = ByteArray.Subbyte(received, Protocol.PacketBytePosition.Data, 2);
            if (ByteArray.Equals(errorCode, new byte[] { 0xde, 0xe2 })) {
                DigiDeviceException ex = new DigiDeviceException($"Bluetooth PIN incorrect.");
                throw ex;
            }
            else {
                DigiDeviceException ex = new DigiDeviceException($"General Error: {ByteArray.ToHexString(received, Protocol.PacketBytePosition.Data, 2)}");
                throw ex;
            }
        }

    }
}