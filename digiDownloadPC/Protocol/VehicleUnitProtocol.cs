using System;
using Tachosys.System;
using static digiDownloadPC.Protocol;

namespace digiDownloadPC
{
    class VehicleUnitProtocol
    {
        const byte Format = 0x80;
        const byte IDEAddress = 0xf0;
        const byte VUAddress = 0xee;

        public static byte[] ResponseHeader = new byte[] { Format, IDEAddress, VUAddress };

        public static byte[] StartCommunications()
        {
            return new byte[] { 0x81, VUAddress, IDEAddress, 0x81, 0xe0 };
        }

        public static byte[] StopCommunications()
        {
            return new byte[] { Format, VUAddress, IDEAddress, 0x01, 0x82, 0xe1 };
        }

        public static byte[] StartDiagnostic()
        {
            return new byte[] { Format, VUAddress, IDEAddress, 0x02, 0x10, 0x81, 0xf1 };
        }

        public static byte[] VerifyBaudRate(byte index)
        {
            var instruction = new ByteArray(9);

            instruction.SetRange(0, new byte[] { Format, VUAddress, IDEAddress });
            instruction[3] = 0x04;
            instruction[4] = 0x87;
            instruction.SetRange(5, new byte[] { 0x01, 0x01, index });
            instruction[8] = Packet.GetChecksum(instruction.ToBytes());

            return instruction.ToBytes();
        }

        public static byte[] TransitionBaudRate()
        {
            var instruction = new ByteArray(8);

            instruction.SetRange(0, new byte[] { Format, VUAddress, IDEAddress });
            instruction[3] = 0x03;
            instruction[4] = 0x87;
            instruction.SetRange(5, new byte[] { 0x02, 0x03 });
            instruction[7] = Packet.GetChecksum(instruction.ToBytes());

            return instruction.ToBytes();
        }

        public static byte[] RequestUpload()
        {
            var instruction = new ByteArray(15);

            instruction.SetRange(0, new byte[] { Format, VUAddress, IDEAddress });
            instruction[3] = 0x0a;
            instruction[4] = 0x35;
            instruction.SetRange(5, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff });
            instruction[14] = Packet.GetChecksum(instruction.ToBytes());

            return instruction.ToBytes();
        }

        public static byte[] TransferDataRequest(byte trep, byte[] data)
        {
            var instruction = new ByteArray(7 + data.Length);

            instruction.SetRange(0, new byte[] { Format, VUAddress, IDEAddress });
            instruction[3] = (byte)(2 + data.Length);
            instruction[4] = 0x36;
            instruction[5] = trep;
            instruction.SetRange(6, data);
            instruction[instruction.Length - 1] = Packet.GetChecksum(instruction.ToBytes());

            return instruction.ToBytes();
        }

        public static byte[] AcknowledgeSubMessage(byte sid, UInt16 msgC)
        {
            var instruction = new ByteArray(9);

            instruction.SetRange(0, new byte[] { Format, VUAddress, IDEAddress });
            instruction[3] = 0x04;
            instruction[4] = 0x83;
            instruction[5] = sid;
            instruction.SetRange(6, ByteArray.FromUInt16(msgC));
            instruction[8] = Packet.GetChecksum(instruction.ToBytes());

            return instruction.ToBytes();
        }

        public static byte[] RequestTransferExit()
        {
            return new byte[] { Format, VUAddress, IDEAddress, 0x01, 0x37, 0x96 };
        }

        public static byte[] ReadDataByIdentifier(byte[] identifier)
        {
            var instruction = new ByteArray(8);

            instruction.SetRange(0, new byte[] { Format, VUAddress, IDEAddress });
            instruction[3] = 0x03;
            instruction[4] = 0x22;
            instruction.SetRange(5, 2, identifier);
            instruction[7] = Packet.GetChecksum(instruction.ToBytes());

            return instruction.ToBytes();
        }
    }
}
