using System;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Tachosys.System
{

    [Serializable, DataContract]
    public struct TimeReal : IComparable, IComparable<TimeReal>, IEquatable<TimeReal>, IXmlSerializable
    {

        private UInt32 value;

        public static readonly TimeReal MinValue = new TimeReal(0);
        public static readonly TimeReal MaxValue = new TimeReal(UInt32.MaxValue);

        public TimeReal(UInt32 seconds)
        {
            this.value = seconds;
        }
        public TimeReal(DateTime value)
        {
            TimeSpan t = value - MinValue;
            this.value = (UInt32)t.TotalSeconds;
        }

        [DataMember]
        public UInt32 Ticks
        {
            get { return this.value; }
            private set { this.value = value; }
        }

        [XmlIgnore]
        public TimeReal Date
        {
            get { return (TimeReal)this.ToDateTime().Date; }
        }

        public static bool operator ==(TimeReal t1, TimeReal t2)
        {
            return t1.value == t2.value;
        }
        public static bool operator !=(TimeReal t1, TimeReal t2)
        {
            return t1.value != t2.value;
        }
        public static bool operator <(TimeReal t1, TimeReal t2)
        {
            return t1.value < t2.value;
        }
        public static bool operator >(TimeReal t1, TimeReal t2)
        {
            return t1.value > t2.value;
        }
        public static bool operator <=(TimeReal t1, TimeReal t2)
        {
            return t1.value <= t2.value;
        }
        public static bool operator >=(TimeReal t1, TimeReal t2)
        {
            return t1.value >= t2.value;
        }

        public static TimeReal First(TimeReal arg0, TimeReal arg1)
        {
            if (arg0.value <= arg1.value)
                return arg0;
            else
                return arg1;
        }

        public static TimeReal Last(TimeReal arg0, TimeReal arg1)
        {
            if (arg0.value >= arg1.value)
                return arg0;
            else
                return arg1;
        }
        public static TimeSpan operator -(TimeReal t1, TimeReal t2)
        {
            return (DateTime)t1 - (DateTime)t2;
        }

        public TimeReal AddDays(int value)
        {
            return new TimeReal((uint)(this.value + (value * 86400)));
        }

        public TimeReal AddHours(int value)
        {
            return new TimeReal((uint)(this.value + (value * 3600)));
        }

        public TimeReal AddMinutes(int value)
        {
            return new TimeReal((uint)(this.value + (value * 60)));
        }

        public TimeReal AddSeconds(int value)
        {
            return new TimeReal((uint)(this.value + value));
        }

        public static implicit operator DateTime(TimeReal t)
        {
            UInt32 seconds = t.value;
            UInt32 minutes = seconds / 60;
            UInt32 remainder = seconds % 60;
            DateTime ret = new DateTime(1970, 1, 1);
            ret = ret.AddMinutes(minutes);
            ret = ret.AddSeconds(remainder);
            return ret;
        }
        public static explicit operator TimeReal(DateTime t)
        {
            return new TimeReal(t);
        }

        public DateTime ToDateTime()
        {
            return Convert.ToDateTime(this);
        }

        public DateTime ToLocalTime()
        {
            return this.ToDateTime().ToLocalTime();
        }

        public string ToLocalString()
        {
            if (value == 0 | value == UInt32.MaxValue) {
                return "";
            }
            else {
                return this.ToLocalTime().ToString();
            }
        }
        public string ToLocalString(string format)
        {
            if (value == 0 | value == UInt32.MaxValue) {
                return "";
            }
            else {
                return this.ToLocalTime().ToString(format);
            }
        }

        public override string ToString()
        {
            if (value == 0 | value == UInt32.MaxValue) {
                return "";
            }
            else {
                return this.ToDateTime().ToString();
            }
        }
        public string ToString(string format)
        {
            if (value == 0 | value == UInt32.MaxValue) {
                return "";
            }
            else {
                return this.ToDateTime().ToString(format);
            }
        }

        public int CompareTo(object obj)
        {
            if (obj is TimeReal || obj is DateTime) {
                TimeReal tr = (TimeReal)obj;

                return this.CompareTo(tr);
            }

            throw new ArgumentException("object is not a TimeReal");
        }
        public int CompareTo(TimeReal other)
        {
            return value.CompareTo(other.value);
        }

        public override bool Equals(object obj)
        {
            if (obj is TimeReal || obj is DateTime) {
                TimeReal tr = (TimeReal)obj;

                return this.Equals(tr);
            }

            throw new ArgumentException("object is not a TimeReal");
        }
        public bool Equals(TimeReal other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            value = (uint)reader.ReadElementContentAsLong();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteValue(value);
        }

    }

}
