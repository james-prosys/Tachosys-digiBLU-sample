using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace digiDownloadPC
{
    partial class Protocol
    {
        public static byte[] AskAcknowledge()
        {
            return new Instruction(Instructions.Acknowledge.Ask).ToBytes();
        }

        public static byte[] ConfigRead(byte section)
        {
            return new Instruction(Instructions.Config.Read, new byte[] { section }).ToBytes();
        }

        public static byte[] ConfigurePort(byte[] data)
        {
            return new Instruction(Instructions.Config.ConfigurePort, data).ToBytes();
        }

        public static byte[] ExitCommandMode(byte[] data)
        {
            return new Instruction(Instructions.Device.ExitCommandMode, data).ToBytes();
        }

    }
}
