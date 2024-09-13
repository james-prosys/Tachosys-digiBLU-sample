using System;
using System.Text;

namespace Tachosys.System
{

    public struct ByteArray
    {

        private byte[] value;

        public ByteArray(byte[] value)
        {
            this.value = new byte[value.Length];
            Array.Copy(value, this.value, value.Length);
        }
        public ByteArray(int length)
        {
            this.value = new byte[length];
        }
        public ByteArray(byte value, int count)
        {
            this.value = new byte[count];
            for (int i = 0; i < count; i++) this.value[i] = value;
        }

        public int Length
        {
            get { return this.value.Length; }
        }

        public void Append(byte[] value)
        {
            int index = this.value.Length;
            Array.Resize(ref this.value, index + value.Length);
            value.CopyTo(this.value, index);
        }
        public void Append(byte[] value, int length)
        {
            int index = this.value.Length;
            Array.Resize(ref this.value, index + length);
            SetRange(index, length, value);
        }

        public void SetRange(int startIndex, byte[] value)
        {
            SetRange(startIndex, value.Length, value);
        }
        public void SetRange(int startIndex, int length, byte[] value)
        {
            if (value.Length < length)
                length = value.Length;
            Array.Copy(value, 0, this.value, startIndex, length);
        }

        public static byte[] Subbyte(byte[] value, int startIndex, int length)
        {
            byte[] ret = new byte[length];
            Array.Copy(value, startIndex, ret, 0, length);
            return ret;
        }
        public byte[] Subbyte(int startIndex, int length)
        {
            return Subbyte(this.value, startIndex, length);
        }

        public byte this[int index]
        {
            get { return this.value[index]; }
            set { this.value[index] = value; }
        }

        public static byte[] FromUInt16(UInt16 value)
        {
            return new byte[] { (byte)((value & 0xff00) >> 8), (byte)(value & 0xff) };
        }
        public static byte[] FromUInt24(UInt32 value)
        {
            return new byte[] { (byte)((value & 0xff0000) >> 16), (byte)((value & 0xff00) >> 8), (byte)(value & 0xff) };
        }
        public static byte[] FromUInt32(UInt32 value)
        {
            return new byte[] { (byte)((value & 0xff000000) >> 24), (byte)((value & 0xff0000) >> 16), (byte)((value & 0xff00) >> 8), (byte)(value & 0xff) };
        }

        public static UInt16 ToUInt8(byte[] value, int startIndex)
        {
            return value[startIndex];
        }
        public static UInt16 ToUInt16(byte[] value, int startIndex)
        {
            return (UInt16)(value[startIndex + 1] + (value[startIndex] * 0x100));
        }
        public static UInt32 ToUInt24(byte[] value, int startIndex)
        {
            return (UInt32)(value[startIndex + 2] + (value[startIndex + 1] * 0x100) + (value[startIndex] * 0x10000));
        }
        public static UInt32 ToUInt32(byte[] value, int startIndex, int length)
        {
            switch (length) {
                case 1:
                    return value[startIndex];
                case 2:
                    return (UInt32)(value[startIndex + 1] + (value[startIndex] * 0x100));
                case 3:
                    return (UInt32)(value[startIndex + 2] + (value[startIndex + 1] * 0x100) + (value[startIndex] * 0x10000));
                case 4:
                    return (UInt32)(value[startIndex + 3] + (value[startIndex + 2] * 0x100) + (value[startIndex + 1] * 0x10000) + (value[startIndex] * 0x1000000L));
                default:
                    throw new ArgumentOutOfRangeException("length");
            }
        }
        public static UInt32 ToUInt32(byte[] value, int startIndex)
        {
            return ToUInt32(value, startIndex, 4);
        }
        public UInt32 ToUInt32(int startindex, int length)
        {
            return ToUInt32(this.value, startindex, length);
        }

        public static DateTime ToFMSDate(byte[] value, int startIndex)
        {
            if ((value[startIndex] == 0 || value[startIndex] == 0xff) && (value[startIndex + 1] == 0 || value[startIndex + 1] == 0xff))
                return DateTime.MinValue;

            int month = value[startIndex];
            int day = (value[startIndex + 1] + 3) >> 2;
            int year = 1985 + value[startIndex + 2];

            return new DateTime(year, month, day);
        }

        public static DateTime ToFMSDateTime(byte[] value, int startIndex)
        {
            if ((value[startIndex + 3] == 0 || value[startIndex + 3] == 0xff) && (value[startIndex + 4] == 0 || value[startIndex + 4] == 0xff))
                return DateTime.MinValue;

            int second = (value[startIndex] + 3) >> 2;
            int minute = value[startIndex + 1];
            int hour = value[startIndex + 2];
            int month = value[startIndex + 3];
            int day = (value[startIndex + 4] + 3) >> 2;
            int year = 1985 + value[startIndex + 5];

            return new DateTime(year, month, day, hour, minute, second);
        }

        public static byte[] FromTimeReal(TimeReal value)
        {
            return FromUInt32(value.Ticks);
        }

        public static string ToBCDString(byte[] value, int startIndex, int length)
        {
            return ToHexString(value, startIndex, length);
        }
        public string ToBCDString(int startIndex, int length)
        {
            return ToHexString(startIndex, length);
        }

        public static string ToHexString(byte[] value)
        {
            return ToHexString(value, 0, value.Length, "");
        }
        public static string ToHexString(byte[] value, string delimiter)
        {
            return ToHexString(value, 0, value.Length, delimiter);
        }
        public static string ToHexString(byte[] value, int startIndex, int length)
        {
            return ToHexString(value, startIndex, length, "");
        }
        public static string ToHexString(byte[] value, int startIndex, int length, string delimiter)
        {
            string[] str = new string[length];
            for (int i = 0; i <= length - 1; i++) {
                str[i] = string.Format("{0:X2}", value[i + startIndex]);
            }
            return string.Join(delimiter, str);
        }
        public string ToHexString(int startIndex, int length)
        {
            return ToHexString(this.value, startIndex, length, "");
        }

        public static string ToIA5String(byte[] value)
        {
            return ToIA5String(value, 0, value.Length);
        }
        public static string ToIA5String(byte[] value, int startIndex, int length)
        {
            if (value[startIndex] == 0xff) return string.Empty;

            Encoding enc = Encoding.ASCII;
            string text = enc.GetString(value, startIndex, length);
            text = TrimFromNullChar(text);
            text = ReplaceUnprintableChars(text, '?');
            return text;
        }
        public string ToIA5String(int startIndex, int length)
        {
            return ToIA5String(this.value, startIndex, length);
        }

        public static string ToString(byte[] value, int startIndex, int length)
        {
            if (value[startIndex] == 0xff) return string.Empty;

            Encoding enc;
            byte codepage = value[startIndex];
            try {
                enc = Encoding.GetEncoding($"iso-8859-{codepage}");
            }
            catch {
                enc = Encoding.GetEncoding("iso-8859-1");
            }
            string text = enc.GetString(value, startIndex + 1, length - 1);
            text = TrimFromNullChar(text);
            text = ReplaceUnprintableChars(text, '?');
            return text;
        }
        public string ToString(int startIndex, int length)
        {
            return ToString(this.value, startIndex, length);
        }

        public byte[] ToBytes()
        {
            byte[] b = new byte[value.Length];
            Array.Copy(value, b, value.Length);
            return b;
        }

        private static string ReplaceUnprintableChars(string text, char replacement)
        {
            char[] chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++) {
                if (chars[i] == '\t')
                    chars[i] = ' ';
                else if (char.IsControl(chars[i]))
                    chars[i] = replacement;
            }
            return new string(chars);
        }
        private static string TrimFromNullChar(string text)
        {
            int index = text.IndexOf('\0');
            if (index >= 0) {
                return text.Substring(0, index);
            }
            return text;
        }
    }
}
