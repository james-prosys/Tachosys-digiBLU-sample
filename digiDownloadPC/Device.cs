using System;
using System.IO.Ports;
using System.Threading;
using Tachosys.System;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace digiDownloadPC
{
    class Device
    {
        readonly SerialPort serialPort;

        protected int insertPos;
        protected byte[] received;
        protected DateTime timeout;

        public event DataEventHandler DataReceived;
        public event DataEventHandler DataSent;

        public Device(string connectionPath)
        {
            serialPort = new SerialPort(connectionPath);
            RequestTimeout = DefaultRequestTimeout;
        }

        public Device(GattCharacteristic send, GattCharacteristic receive)
        {

            RequestTimeout = DefaultRequestTimeout;
        }

        protected void OnDataReceived(DataEventArgs e)
        {
            DataReceived?.Invoke(this, e);
        }
        protected void OnDataSent(DataEventArgs e)
        {
            DataSent?.Invoke(this, e);
        }

        public int DefaultRequestTimeout
        {
            get { return 2; }
        }
        public int RequestTimeout { get; set; }

        public bool Connect()
        {
            Disconnect();

            try {
                serialPort.BaudRate = 115200;
                serialPort.DataBits = 8;
                serialPort.StopBits = StopBits.One;
                serialPort.Parity = Parity.None;

                serialPort.Open();

                return true;
            }
            catch (Exception ex) {
                throw new ConnectFailureException(ex);
            }
        }

        public void Disconnect()
        {
            try {
                serialPort.Close();
                serialPort.Dispose();
            }
            catch { }
        }

        public void Send(byte[] data)
        {
            OnDataSent(new DataEventArgs(data));

            try {
                serialPort.DiscardInBuffer();
                serialPort.Write(data, 0, data.Length);
            }
            catch (Exception ex) {
                throw new SendFailureException(ex);
            }
        }

        public byte[] ExecuteVUCommand(byte[] command)
        {
            return ExecuteVUCommand(command, false);
        }

        public byte[] ExecuteVUCommand(byte[] command, bool ignoreRequestOutOfRange)
        {
            OnDataSent(new DataEventArgs(command));

            try {
                serialPort.DiscardInBuffer();
                serialPort.Write(command, 0, command.Length);
            }
            catch (Exception ex) {
                throw new SendFailureException(ex);
            }

            timeout = DateTime.Now.AddSeconds(RequestTimeout);

            int? packetLength;

            while (true) {

                insertPos = 0;
                received = new byte[1024];
                packetLength = null;

                while (true) {
                    if (serialPort.BytesToRead > 0) {
                        byte b = (byte)serialPort.ReadByte();

                        switch (insertPos) {
                            case 0:
                            case 1:
                            case 2:
                                if (b == VehicleUnitProtocol.ResponseHeader[insertPos]) {
                                    received[insertPos] = b;
                                    insertPos++;
                                }
                                break;
                            case 3:
                                received[insertPos] = b;
                                insertPos++;
                                packetLength = (b & 0xff) + VehicleUnitProtocol.ResponseHeader.Length + 2;
                                break;
                            default:
                                if (packetLength != null) {
                                    received[insertPos] = b;
                                    insertPos++;
                                    if (insertPos == packetLength) goto processReceived;
                                }
                                break;
                        }
                    }
                    else if (DateTime.Now > timeout) {
                        throw new TachographException($"No response from Tachograph received. Timeout reached.", TachographException.TimeoutErrorCode );
                    }
                    else
                        Thread.Sleep(1);
                }

            processReceived:
                Array.Resize(ref received, (int)packetLength);

                DataEventArgs args = new DataEventArgs((byte[])received.Clone());
                OnDataReceived(args);

                if (received[3] > 0 && received[4] == (byte)0x7f) {
                    if (received[6] == (byte)0x78) {
                        timeout = timeout.AddMinutes(20);
                    }
                    else {
                        String errorCode = ByteArray.ToHexString(received, 4, 3, " ");
                        if ((errorCode.Equals("7F 22 22") || errorCode.Equals("7F 22 31")) && ignoreRequestOutOfRange)
                            return new byte[] { };
                        else
                            throw new TachographException($"Negative response received from Tachograph: {errorCode}", ByteArray.Subbyte(received, 4, 3));
                    }
                }
                else
                    break;
            }

            byte[] data = new byte[(int)packetLength - VehicleUnitProtocol.ResponseHeader.Length - 2];
            Array.Copy(received, VehicleUnitProtocol.ResponseHeader.Length + 1, data, 0, data.Length);

            return data;

        }

        public byte[] ExecuteCommand(byte[] command)
        {
            OnDataSent(new DataEventArgs(command));

            try {
                serialPort.DiscardInBuffer();
                serialPort.Write(command, 0, command.Length);
            }
            catch (Exception ex) {
                throw new SendFailureException(ex);
            }

            WaitForResponse();

            return received;
        }

        private bool LocateHeader()
        {
            byte buffer;
            while (true) {
                int length = serialPort.BytesToRead;
                if (length > 0) {
                    for (int i = 1; i <= length; i++) {
                        buffer = (byte)serialPort.ReadByte();
                        if (buffer == Protocol.Packet.Header) {
                            received[insertPos] = Protocol.Packet.Header;
                            insertPos += 1;
                            return true;
                        }
                    }
                }

                if (DateTime.Now > timeout) {
                    throw new AcknowledgeTimeoutException();
                }

                Thread.Sleep(1);
            }
        }

        private bool LocateLength()
        {
            while (true) {
                int length = serialPort.BytesToRead;
                if (length > 0) {
                    serialPort.Read(received, insertPos, length);
                    insertPos += length;
                }
                if (insertPos >= Protocol.PacketBytePosition.Data) {
                    return true;
                }
                if (DateTime.Now > timeout) {
                    throw new AcknowledgeTimeoutException();
                }

                Thread.Sleep(1);
            }
        }

        private void ReadResponse()
        {
            if (!LocateHeader()) {
                return;
            }
            if (!LocateLength()) {
                return;
            }

            int packetLength = Protocol.PacketBytePosition.Data + ByteArray.ToInt16(received, Protocol.PacketBytePosition.Length) + 1;
            while (true) {
                int length = serialPort.BytesToRead;
                if (length > 0) {
                    serialPort.Read(received, insertPos, length);
                    insertPos += length;
                }
                if (insertPos >= packetLength) {
                    Array.Resize(ref received, packetLength);
                    break;
                }

                if (DateTime.Now > timeout) {
                    throw new AcknowledgeTimeoutException();
                }

                Thread.Sleep(1);
            }

            ProcessReceived();
        }

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
            DigiDeviceException ex = new DigiDeviceException($"General Error: {ByteArray.ToHexString(received, Protocol.PacketBytePosition.Data, 2)}");
            throw ex;
        }

        private void WaitForResponse()
        {
            insertPos = 0;
            received = new byte[1024];

            timeout = DateTime.Now.AddSeconds(RequestTimeout);
            while (true) {
                try {
                    int length = serialPort.BytesToRead;
                    if (length > 0) {
                        break;
                    }
                }
                catch (Exception ex) {
                    throw new SendFailureException(ex);
                }

                if (DateTime.Now > timeout) {
                    throw new AcknowledgeTimeoutException();
                }

                Thread.Sleep(1);
            }

            ReadResponse();
        }

    }
}
