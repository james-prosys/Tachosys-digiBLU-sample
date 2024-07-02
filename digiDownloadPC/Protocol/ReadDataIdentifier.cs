using System;

namespace digiDownloadPC
{
    class ReadDataIdentifier
    {
        public static byte[] TachographManufacturer = new byte[] { 0xf1, 0x8a };
        public static byte[] TachographPartNumber = new byte[] { 0xf1, 0x92 };
        public static byte[] TachographSoftwareNumber = new byte[] { 0xf1, 0x94 };

        public static byte[] TachographCardSlot1 = new byte[] { 0xf9, 0x30 };
        public static byte[] TachographCardSlot2 = new byte[] { 0xf9, 0x33 };
        public static byte[] DriverIdentificationSlot1 = new byte[] { 0xf9, 0x16 };
        public static byte[] DriverIdentificationSlot2 = new byte[] { 0xf9, 0x17 };

        public static byte[] TachographNextMandatoryDownloadDate = new byte[] { 0xf9, 0x9f };
        public static byte[] NextCalibrationDate = new byte[] { 0xf9, 0x22 };

        public static byte[] DriverNameSlot1 = new byte[] { 0xf9, 0x31 };
        public static byte[] DriverNameSlot2 = new byte[] { 0xf9, 0x32 };

        public static byte[] VehicleIdentificationNumber = new byte[] { 0xf1, 0x90 };
        public static byte[] RegisteringMemberState = new byte[] { 0xf9, 0x7d };
        public static byte[] VehicleRegistration = new byte[] { 0xf9, 0x7e };

        public static byte[] Driver1CardExpiryDate = new byte[] { 0xf9, 0x9d };
        public static byte[] Driver1CardNextMandatoryDownloadDate = new byte[] { 0xf9, 0x9e };
        public static byte[] Driver2CardExpiryDate = new byte[] { 0xf9, 0x8b };
        public static byte[] Driver2CardNextMandatoryDownloadDate = new byte[] { 0xf9, 0x8c };

        public static byte[] CurrentDateTime = new byte[] { 0xf9, 0x0b };
        public static byte[] TimeLeftToExpiredCard = new byte[] { 0xf9, 0xa8 };
        public static byte[] TimeLeftToDownloadCard = new byte[] { 0xf9, 0xa9 };

        public static byte[] StoneridgeDDSFormat = new byte[] { 0xfe, 0x34 };

    }
}
