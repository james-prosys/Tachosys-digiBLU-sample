using System;
using System.Net.Sockets;
using Tachosys.System;

namespace digiDownloadPC
{
    partial class Protocol
    {

        sealed class Instruction
        {

            private byte[] instruction;

            public Instruction(byte command)
            {
                instruction = new byte[] { Packet.Header, command, 0x0, 0x0, 0x0 };
                instruction[instruction.Length - 1] = Packet.GetChecksum(instruction);
            }
            public Instruction(byte command, byte[] data)
            {
                byte[] length = ByteArray.FromUInt16((UInt16)data.Length);

                instruction = new byte[data.Length + PacketBytePosition.Data + 1];
                instruction[PacketBytePosition.Header] = Packet.Header;
                instruction[PacketBytePosition.Instruction] = command;

                length.CopyTo(instruction, PacketBytePosition.Length);
                data.CopyTo(instruction, PacketBytePosition.Data);
                instruction[instruction.Length - 1] = Packet.GetChecksum(instruction);
            }

            public byte[] ToBytes()
            {
                return instruction;
            }

        }

        internal static class Instructions
        {
            public static class Groups
            {
                public const byte Acknowledge = 0xa;
            }

            public static class Acknowledge
            {
                public const byte OK = 0xa0;
                public const byte Ask = 0xaa;
                public const byte Error = 0xae;
                public const byte Failure = 0xaf;
            }

            public static class Config
            {
                public static byte Read = 0xca;
                public static byte ConfigurePort = 0xc9;
            }

            public static class Device
            {
                public static byte Hello = 0xd3;
                public static byte Authenticate = 0xd4;
                public static byte ExitCommandMode = 0xde;
            }
        }

        internal static class PacketBytePosition
        {
            public const int Header = 0;
            public const int Instruction = 1;
            public const int Length = 2;
            public const int Data = 4;
        }

        internal static class Packet
        {
            public const byte Header = 0x55;

            public static byte GetChecksum(byte[] buffer)
            {
                int checksum = 0;
                foreach (byte item in buffer) {
                    checksum = (checksum + item) % 0x100;
                }
                return (byte)checksum;
            }

            public static bool CheckChecksum(byte[] packet)
            {
                byte[] buffer = new byte[packet.Length - 1];
                Array.Copy(packet, buffer, packet.Length - 1);
                return (packet[packet.Length - 1] == GetChecksum(buffer));
            }
        }

        internal static class ConfigSections
        {
            public const byte DeviceIdentity = 0x1;
        }

    }
}
