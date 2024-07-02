using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Tachosys.System;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace digiDownloadPC
{
    internal class BluetoothLEConnection : BluetoothConnectionBase
    {
        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        static Guid NORDIC_NUS_SERVICE_ID = new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e");
        static Guid NORDIC_NUS_TX_ID = new Guid("6e400003-b5a3-f393-e0a9-e50e24dcca9e");
        static Guid NORDIC_NUS_RX_ID = new Guid("6e400002-b5a3-f393-e0a9-e50e24dcca9e");

        private ConcurrentQueue<byte> InBuffer = new ConcurrentQueue<byte>();

        private BluetoothLEDevice bluetoothLEDevice;
        private GattDeviceService gattDeviceService;
        private GattCharacteristic characteristicTX, characteristicRX;

        public BluetoothLEConnection(string deviceId) : base(deviceId) { }

        public override int DefaultRequestTimeout
        {
            get { return 3; }
        }

        public override async Task<bool> Connect()
        {
            Disconnect();

            try {
                bluetoothLEDevice = await BluetoothLEDevice.FromIdAsync(ConnectionPath);
                if (bluetoothLEDevice == null) return false;

                GattDeviceServicesResult servicesResult = await bluetoothLEDevice.GetGattServicesForUuidAsync(NORDIC_NUS_SERVICE_ID);
                if (servicesResult.Status != GattCommunicationStatus.Success) {
                    return false;
                }
                if (servicesResult.Services.Count == 0) {
                    return false;
                }

                gattDeviceService = servicesResult.Services[0];

                var accessStatus = await gattDeviceService.RequestAccessAsync();
                if (accessStatus != DeviceAccessStatus.Allowed) {
                    return false;
                }

                var characteristicsResult = await gattDeviceService.GetCharacteristicsForUuidAsync(NORDIC_NUS_TX_ID);
                if (characteristicsResult.Status == GattCommunicationStatus.Success && characteristicsResult.Characteristics.Count > 0) characteristicTX = characteristicsResult.Characteristics[0];
                characteristicsResult = await gattDeviceService.GetCharacteristicsForUuidAsync(NORDIC_NUS_RX_ID);
                if (characteristicsResult.Status == GattCommunicationStatus.Success && characteristicsResult.Characteristics.Count > 0) characteristicRX = characteristicsResult.Characteristics[0];

                if (characteristicTX == null || characteristicRX == null) {
                    return false;
                }

                characteristicTX.ValueChanged += Receive_ValueChanged;

                var status = await characteristicTX.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (status != GattCommunicationStatus.Success)
                    return false;

                return true;
            }
            catch (Exception ex) {
                throw new ConnectFailureException(ex);
            }
        }

        public override void Disconnect()
        {
            base.Disconnect();

            try {
                if (bluetoothLEDevice != null) {
                    global::System.Diagnostics.Debug.WriteLine($"BluetoothLE Disconnect {ConnectionPath}");

                    if (characteristicTX != null) {
                        _ = characteristicTX.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);

                        characteristicTX.ValueChanged -= Receive_ValueChanged;

                        characteristicTX = null;
                    }
                    characteristicRX = null;

                    gattDeviceService?.Dispose();
                    gattDeviceService = null;

                    bluetoothLEDevice.Dispose();
                    bluetoothLEDevice = null;

                    global::System.Diagnostics.Debug.WriteLine($"BluetoothLE Disconnected from {ConnectionPath}");
                }
            }
            catch { }
        }

        public override async Task<byte[]> ExecuteVUCommand(byte[] command, bool ignoreRequestOutOfRange)
        {
            await semaphoreSlim.WaitAsync();
            try {
                LastCommandTime = DateTime.Now;

                OnDataSent(new DataEventArgs(command));

                try {
                    InBuffer = new ConcurrentQueue<byte>();
                    IBuffer buffer = WindowsRuntimeBufferExtensions.AsBuffer(command, 0, command.Length);
                    var result = await characteristicRX.WriteValueAsync(buffer);

                    if (result != GattCommunicationStatus.Success)
                        throw new ApplicationException($"Send attempt failed - {result}.");
                }
                catch (Exception ex) {
                    throw new SendFailureException(ex);
                }

                int? packetLength;
                timeout = DateTime.Now.AddSeconds(RequestTimeout);

                while (true) {

                    insertPos = 0;
                    received = new byte[1024];
                    packetLength = null;

                    while (true) {
                        if (InBuffer.TryDequeue(out byte b)) {

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
                                    packetLength = b + VehicleUnitProtocol.ResponseHeader.Length + 2;
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
                            throw new TachographException($"No response from Tachograph received. Timeout reached.", TachographException.TimeoutErrorCode);
                        }
                        else
                            Thread.Sleep(1);
                    }

                processReceived:
                    Array.Resize(ref received, (int)packetLength);

                    DataEventArgs args = new DataEventArgs((byte[])received.Clone());
                    OnDataReceived(args);

                    if (received[3] > 0 && received[4] == 0x7f) {
                        if (received[6] == 0x78) {
                            timeout = timeout.AddMinutes(20);
                        }
                        else {
                            string errorCode = ByteArray.ToHexString(received, 4, 3, " ");
                            if ((errorCode.Equals("7F 22 12") || errorCode.Equals("7F 22 22") || errorCode.Equals("7F 22 31")) && ignoreRequestOutOfRange)
                                return new byte[] { };
                            else
                                throw new TachographException($"Negative response received from Tachograph: {errorCode}", ByteArray.Subbyte(received, 4, 3));
                        }
                    }
                    else
                        break;
                }

                byte[] data = new byte[received.Length - VehicleUnitProtocol.ResponseHeader.Length - 2];
                Array.Copy(received, VehicleUnitProtocol.ResponseHeader.Length + 1, data, 0, data.Length);

                return data;
            }
            finally { semaphoreSlim.Release(); }
        }

        public override async Task<byte[]> ExecuteCommand(byte[] data)
        {
            await semaphoreSlim.WaitAsync();
            try {
                LastCommandTime = DateTime.Now;

                OnDataSent(new DataEventArgs(data));

                try {
                    InBuffer = new ConcurrentQueue<byte>();
                    IBuffer buffer = WindowsRuntimeBufferExtensions.AsBuffer(data, 0, data.Length);
                    var result = await characteristicRX.WriteValueAsync(buffer);

                    if (result != GattCommunicationStatus.Success)
                        throw new ApplicationException($"Send attempt failed - {result}.");
                }
                catch (Exception ex) {
                    throw new SendFailureException(ex);
                }

                WaitForResponse();

                return received;
            }
            finally { semaphoreSlim.Release(); }
        }

        public override async Task Send(byte[] data)
        {
            await semaphoreSlim.WaitAsync();
            try {
                LastCommandTime = DateTime.Now;

                OnDataSent(new DataEventArgs(data));

                try {
                    InBuffer = new ConcurrentQueue<byte>();
                    IBuffer buffer = WindowsRuntimeBufferExtensions.AsBuffer(data, 0, data.Length);
                    var result = await characteristicRX.WriteValueAsync(buffer);

                    if (result != GattCommunicationStatus.Success)
                        throw new ApplicationException($"Send attempt failed - {result}.");
                }
                catch (Exception ex) {
                    throw new SendFailureException(ex);
                }
            }
            finally { semaphoreSlim.Release(); }
        }

        private bool LocateHeader()
        {
            while (true) {
                if (InBuffer.TryDequeue(out byte buffer)) {
                    if (buffer == Protocol.Packet.Header) {
                        received[insertPos] = Protocol.Packet.Header;
                        insertPos++;
                        return true;
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
                if (InBuffer.TryDequeue(out byte buffer)) {
                    received[insertPos] = buffer;
                    insertPos++;
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
                if (InBuffer.TryDequeue(out byte buffer)) {
                    received[insertPos] = buffer;
                    insertPos++;
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

        private void WaitForResponse()
        {
            insertPos = 0;
            received = new byte[1024];

            timeout = DateTime.Now.AddSeconds(RequestTimeout);

            while (true) {
                try {
                    if (!InBuffer.IsEmpty)
                        break;
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

        private void Receive_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var reader = DataReader.FromBuffer(args.CharacteristicValue);
            byte[] buffer = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(buffer);

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"<= {ByteArray.ToHexString(buffer, " ")}");

            foreach (var b in buffer) {
                InBuffer.Enqueue(b);
            }
        }

    }
}
