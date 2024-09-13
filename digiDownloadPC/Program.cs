using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ProSys.Utilities;
using Tachosys.System;
using Windows.Devices.Enumeration;

namespace digiDownloadPC
{
    class Program
    {
        static string SerialNumber;
        static UInt16 BluetoothPIN;

        static DeviceWatcher deviceWatcher;

        static BluetoothConnectionBase connection = null;
        static string port = null;
        static string deviceId = null;

        static TachographManufacturer tachographManufacturer;
        static decimal tachographVersion;
        static bool tachographGen2;
        static CardPresent slot1;
        static CardPresent slot2;

        static byte[] TREPS;
        static byte slotNumber;

        static TimeReal activityStartDate;
        static TimeReal activityEndDate;

        enum TachographManufacturer
        {
            Unknown,
            ASELSAN = 0x15,
            Intellic = 0x53,
            ContinentalAutomotive = 0xa1,
            StoneridgeElectronics = 0xa2,
        }

        enum CardPresent
        {
            Unknown = -1,
            CardNotPresent,
            DriverCard,
            WorkshopCard,
            ControlCard,
            CompanyCard
        }

        static async Task Main(string[] args)
        {
            Task.WaitAll(new [] {
                Task.Run(() => { LocateBluetoothDevice(); }),
                Task.Run(() => { StartBluetoothLEDiscovery(); })
            });

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Wait for device choice...");

            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (string.IsNullOrEmpty(port) || string.IsNullOrEmpty(deviceId)) {
                Thread.Sleep(1);

                if (DateTime.UtcNow > timeout) break;
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Connect to:");
            Console.WriteLine("Press [S] for Serial Bluetooth");
            Console.WriteLine("Press [L] for Bluetooth LE");
            Console.WriteLine("Press [X] to exit");
            var choice = Console.ReadKey();
            Console.WriteLine();

            StopBluetoothLEDiscovery();

            if (choice.Key == ConsoleKey.S)
                connection = new SerialBluetoothConnection(port);
            else if (choice.Key == ConsoleKey.L) {
                while (true) {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("Enter PIN:");
                    var pin = Console.ReadLine();
                    if (UInt16.TryParse(pin, out BluetoothPIN)) break;
                    if (pin == "x" || pin == "X") return;
                }
                connection = new BluetoothLEConnection(deviceId);
            }
            else
                return;

            connection.DataSent += (s, e) => {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} => {ByteArray.ToHexString(e.Data, " ")}");
            };
            connection.DataReceived += (s, e) => {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} <= {ByteArray.ToHexString(e.Data, " ")}");
            };

            await connection.Connect();

            await GetDeviceIdentity();

            await PrepareKLine();

            await GetStaticKLine();

            await PollKLine();

            // Close K-Line
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Stop Communication");
            await connection.ExecuteVUCommand(VehicleUnitProtocol.StopCommunications());

            // Check what is available to be downloaded
            if (slot1 == CardPresent.Unknown && slot2 == CardPresent.Unknown) {
                // Assume K-Line wasn't fully supported so go with fallback of allow attempt of download of vehicle unit and slot 1 driver card.
                slot1 = CardPresent.DriverCard;
                slot2 = CardPresent.CompanyCard;
            }

            bool enableVehicleDownload = false;
            bool enableSlot1Download = false;
            bool enableSlot2Download = false;

            switch (slot1) {
                case CardPresent.CardNotPresent:
                    enableVehicleDownload = (slot2 == CardPresent.WorkshopCard || slot2 == CardPresent.ControlCard || slot2 == CardPresent.CompanyCard);
                    break;
                case CardPresent.DriverCard:
                    if (slot2 == CardPresent.ControlCard || slot2 == CardPresent.CompanyCard) {
                        enableSlot1Download = true;
                        enableVehicleDownload = true;
                    }
                    else if (slot2 == CardPresent.CardNotPresent) {
                        enableSlot1Download = (tachographManufacturer == TachographManufacturer.StoneridgeElectronics && tachographVersion >= 7.3m) ||
                                (tachographManufacturer == TachographManufacturer.ContinentalAutomotive && tachographVersion >= 1.4m) ||
                                (tachographManufacturer == TachographManufacturer.Intellic && tachographVersion >= 4.08m) ||
                                tachographManufacturer == TachographManufacturer.ASELSAN;

                    }
                    else if (slot2 == CardPresent.DriverCard) {
                        enableSlot1Download = (tachographManufacturer == TachographManufacturer.StoneridgeElectronics && tachographVersion >= 8.1m) ||
                                (tachographManufacturer == TachographManufacturer.ContinentalAutomotive && tachographGen2) ||
                                (tachographManufacturer == TachographManufacturer.Intellic && tachographGen2);
                    }
                    break;
                case CardPresent.WorkshopCard:
                    enableSlot1Download = (slot2 == CardPresent.CardNotPresent);
                    enableVehicleDownload = true;
                    break;
                case CardPresent.ControlCard:
                case CardPresent.CompanyCard:
                    enableVehicleDownload = (slot2 == CardPresent.CardNotPresent || slot2 == CardPresent.DriverCard);
                    break;
            }

            if (slot2 == CardPresent.DriverCard)
                enableSlot2Download = (tachographManufacturer == TachographManufacturer.StoneridgeElectronics && tachographVersion >= 8.1m) ||
                        (tachographManufacturer == TachographManufacturer.ContinentalAutomotive && tachographGen2) ||
                        (tachographManufacturer == TachographManufacturer.Intellic && tachographGen2);

            var list = new List<string>();
            if (enableVehicleDownload) list.Add("Vehicle Unit");
            if (enableSlot1Download) list.Add("Driver card in slot 1");
            if (enableSlot2Download) list.Add("Driver card in slot 2");
            var message = list.Count > 0 ? string.Join(", ", list) : "None";

            while (true) {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"Downloads available: {message}");
                Console.WriteLine("Choose download to attempt:");
                if (enableVehicleDownload) Console.WriteLine("Press V for vehicle unit");
                if (enableSlot1Download) Console.WriteLine("Press 1 for slot 1");
                if (enableSlot2Download) Console.WriteLine("Press 2 for slot 2");
                Console.WriteLine("Press X to quit.");

                choice = Console.ReadKey();
                Console.WriteLine();

                if (choice.Key == ConsoleKey.X) break;
                else if (choice.Key == ConsoleKey.V) {
                    var t = new List<byte>();
                    Console.WriteLine("Download Overview:        Y");
                    t.Add(0x01);

                    Console.Write("Download Activities:      ");
                    if (Console.ReadKey().Key == ConsoleKey.Y) {
                        t.Add(0x02);
                        Console.WriteLine();

                        string s;
                        DateTime startDate, endDate;
                        do {
                            Console.WriteLine("Enter activity range start date:");
                            s = Console.ReadLine();
                        } while (!DateTime.TryParse(s, out startDate));
                        do {
                            Console.WriteLine("Enter activity range end date:");
                            s = Console.ReadLine();
                        } while (!DateTime.TryParse(s, out endDate));

                        activityStartDate = (TimeReal)startDate;
                        activityEndDate = (TimeReal)endDate;
                    }
                    Console.WriteLine();

                    Console.Write("Download Events & Faults: ");
                    if (Console.ReadKey().Key == ConsoleKey.Y) t.Add(0x03);
                    Console.WriteLine();

                    Console.Write("Download Detailed Speed:  ");
                    if (Console.ReadKey().Key == ConsoleKey.Y) t.Add(0x04);
                    Console.WriteLine();

                    Console.WriteLine("Download Technical Data:  Y");
                    t.Add(0x05);

                    TREPS = t.ToArray();

                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("Attempting vehicle unit download...");
                }
                else if (choice.Key == ConsoleKey.D1) {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("Attempting card download from slot 1...");
                    TREPS = new byte[] { 0x06 };
                    slotNumber = 1;
                }
                else if (choice.Key == ConsoleKey.D2) {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("Attempting card download from slot 2...");
                    TREPS = new byte[] { 0x06 };
                    slotNumber = 2;
                }

                await Download();
            }

            connection.Disconnect();
        }

        private static async Task GetDeviceIdentity()
        {
            // Guard Time
            Thread.Sleep(500);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Break into command mode");
            await connection.Send(new byte[] { 0x2b, 0x2b, 0x2b });

            // Guard Time
            Thread.Sleep(500);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Ask Acknowledge");
            await connection.ExecuteCommand(Protocol.AskAcknowledge());

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Get Device Identity");
            var data = await connection.ExecuteCommand(Protocol.ConfigRead(Protocol.ConfigSections.DeviceIdentity));

            SerialNumber = ByteArray.ToIA5String(data, 0, 26);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Device serial number: {SerialNumber}");
        }

        private static async Task PrepareKLine()
        {
            // Guard Time
            Thread.Sleep(500);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Prepare K Line");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Break into command mode");
            await connection.Send(new byte[] { 0x2b, 0x2b, 0x2b });

            // Guard Time
            Thread.Sleep(500);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Ask Acknowledge");
            await connection.ExecuteCommand(Protocol.AskAcknowledge());

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Switch to K-Line");
            await connection.ExecuteCommand(Protocol.ConfigurePort(new byte[] { 0x4B }));

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Return to pass-through mode");
            await connection.ExecuteCommand(Protocol.ExitCommandMode(ByteArray.FromUInt16(BluetoothPIN)));

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Start Communication");
            await connection.ExecuteVUCommand(VehicleUnitProtocol.StartCommunications());

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Start diagnostic");
            await connection.ExecuteVUCommand(VehicleUnitProtocol.StartDiagnostic());
        }

        private static async Task GetStaticKLine()
        {
            DateTime nextDownloadDate = DateTime.MinValue;
            DateTime nextCalibrationDate = DateTime.MinValue;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Get Static K Line");

            // F18A - Get Tachograph Manufacturer
            byte[] data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.TachographManufacturer));
            string manufacturer;
            if ((data[3] & 0xff) <= 0x10)
                manufacturer = ByteArray.ToString(data, 3, 36).Trim();
            else
                manufacturer = ByteArray.ToIA5String(data, 3, data.Length - 3);

            if (manufacturer.Contains("Continental") || manufacturer.Contains("Siemens"))
                tachographManufacturer = TachographManufacturer.ContinentalAutomotive;
            else if (manufacturer.Contains("Stoneridge"))
                tachographManufacturer = TachographManufacturer.StoneridgeElectronics;
            else if (manufacturer.ToLower().Contains("intellic"))
                tachographManufacturer = TachographManufacturer.Intellic;
            else if (manufacturer.ToUpper().Contains("ASELSAN"))
                tachographManufacturer = TachographManufacturer.ASELSAN;
            else
                tachographManufacturer = TachographManufacturer.Unknown;

            // F192 - Get SystemSupplierECUHardwareNumber (vuPartNumber)
            data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.TachographPartNumber));
            string vuPartNumber = ByteArray.ToIA5String(data, 3, data.Length - 3).Trim();

            // F194 - Get SystemSupplierECUSoftwareNumber (vuSoftwareVersion)
            data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.TachographSoftwareNumber));
            string vuSoftwareNumber = ByteArray.ToIA5String(data, 3, data.Length - 3).Trim();

            tachographGen2 = false;

            try {
                if (tachographManufacturer == TachographManufacturer.StoneridgeElectronics) {
                    // prototype has 900773
                    // first release training tachograph has 900784
                    if (vuPartNumber.Contains("900773") || vuPartNumber.Contains("900784")) {
                        tachographVersion = 8.1m;
                        tachographGen2 = true;
                    }
                    // prototype has 900588
                    // first release training tachograph has 900653
                    else if (vuPartNumber.Contains("900588") || vuPartNumber.Contains("900653")) {
                        tachographVersion = 8.0m;
                        tachographGen2 = true;
                    }
                    else if (vuPartNumber.Contains("R"))
                        tachographVersion = decimal.Parse(vuPartNumber.Substring(vuPartNumber.IndexOf("R") + 1));
                    else if (vuPartNumber.Contains("T"))
                        tachographVersion = decimal.Parse(vuPartNumber.Substring(vuPartNumber.IndexOf("T") + 1));
                }
                else if (tachographManufacturer == TachographManufacturer.ContinentalAutomotive) {
                    tachographVersion = decimal.Parse(vuSoftwareNumber) / 10.0m;
                    tachographGen2 = tachographVersion >= 4.0m;
                }
                else if (tachographManufacturer == TachographManufacturer.Intellic) {
                    if (vuPartNumber.StartsWith("EFAS-")) {
                        string version = vuPartNumber.Substring(5, vuPartNumber.IndexOf(" ", 5) - 5);
                        tachographVersion = ParseIntellicVersion(version);
                        tachographGen2 = tachographVersion >= 4.10m;
                    }
                }
                else if (tachographManufacturer== TachographManufacturer.ASELSAN) {
                    tachographVersion = decimal.Parse(vuSoftwareNumber);
                    tachographGen2 = false;
                }
            }
            catch {
                tachographVersion = 0;
            }

            // F190 - Get Vehicle Identification Number
            data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.VehicleIdentificationNumber));
            string VIN = ByteArray.ToIA5String(data, 3, 17).Trim();
            // F97D - Get Registering Member State
            data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.RegisteringMemberState));
            string rms = Encoding.GetEncoding("iso-8859-1").GetString(ByteArray.Subbyte(data, 3, 3)).Trim();
            // F97E - Get Vehicle Registration
            data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.VehicleRegistration));
            string VRN = ByteArray.ToString(data, 3, 14).Trim();

            // F99F - Get TachographNextMandatoryDownloadDate
            data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.TachographNextMandatoryDownloadDate), true);
            if (data.Length > 0) nextDownloadDate = ByteArray.ToFMSDate(data, 3);
            // F922 - Get NextCalibrationDate
            data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.NextCalibrationDate), true);
            if (data.Length > 0) nextCalibrationDate = ByteArray.ToFMSDate(data, 3);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Tachograph manufacturer: {tachographManufacturer}");
            Console.WriteLine($"vuPartNumber: {vuPartNumber}, vuSoftwareVersion: {vuSoftwareNumber}, Version: {tachographVersion}, Generation: {(tachographGen2 ? "2" : "1")}");
            Console.WriteLine($"VIN: {VIN}, RMS: {rms}, VRN: {VRN}");
            Console.WriteLine($"Next download date: {nextDownloadDate:d}");
            Console.WriteLine($"Next calibration date: {nextCalibrationDate:d}");

        }

        private static async Task PollKLine()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Poll K Line");

            // F930 - Get Slot 1 Tachograph Card
            slot1 = CardPresent.Unknown;
            byte[] data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.TachographCardSlot1), true);
            if (data.Length > 0)
                slot1 = (CardPresent)data[3];

            // F933 - Get Slot 2 Tachograph Card
            slot2 = CardPresent.Unknown;
            data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.TachographCardSlot2), true);
            if (data.Length > 0)
                slot2 = (CardPresent)data[3];

            string slot1cardNumber = null; string slot1DriverFirstnames = null; string slot1DriverSurname = null;
            string slot2cardNumber = null; string slot2DriverFirstnames = null; string slot2DriverSurname = null;

            if (slot1 > 0) {
                // F916 - Get Driver Identification
                data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.DriverIdentificationSlot1), true);
                if (data.Length == 22)
                    slot1cardNumber = ByteArray.ToIA5String(data, 6, 14);
                else if (data.Length > 0)
                    slot1cardNumber = ByteArray.ToIA5String(data, 3, 14);

                // F931 - Get Driver Name
                data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.DriverNameSlot1), true);
                if (data.Length > 0) {
                    slot1DriverSurname = ByteArray.ToString(data, 3, 36).Trim();
                    slot1DriverFirstnames = ByteArray.ToString(data, 39, 36).Trim();
                }
            }
            if (slot2 > 0) {
                // F917 - Get Driver Identification
                data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.DriverIdentificationSlot2), true);
                if (data.Length == 22)
                    slot2cardNumber = ByteArray.ToIA5String(data, 6, 14);
                else if (data.Length > 0)
                    slot2cardNumber = ByteArray.ToIA5String(data, 3, 14);

                // F932 - Get Driver Name
                data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.DriverNameSlot2), true);
                if (data.Length > 0) {
                    slot2DriverSurname = ByteArray.ToString(data, 3, 36).Trim();
                    slot2DriverFirstnames = ByteArray.ToString(data, 39, 36).Trim();
                }
            }

            DateTime Driver1CardExpiryDate = DateTime.MinValue;
            DateTime Driver1NextDownloadDate = DateTime.MinValue;
            DateTime Driver2CardExpiryDate = DateTime.MinValue;
            DateTime Driver2NextDownloadDate = DateTime.MinValue;

            int format = -1;

            if (tachographManufacturer == TachographManufacturer.StoneridgeElectronics) {
                format = 0;
                // FE34 - Check DDS Format
                data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.StoneridgeDDSFormat), true);
                if (data.Length > 0) format = data[3];
            }

            if (format == 0) {
                // F90B - Get CurrentDateTime
                data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.CurrentDateTime));
                DateTime currentDate = ByteArray.ToFMSDateTime(data, 3);
                // F9A8 - Get TimeLeftToExpiredCard
                data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.TimeLeftToExpiredCard), true);
                if (data.Length > 0 && data[3] < 0xfa) Driver1CardExpiryDate = currentDate.AddDays(data[3]);
                // F9A9 - Get TimeLeftToDownloadCard
                data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.TimeLeftToDownloadCard), true);
                if (data.Length > 0 && data[3] < 0xfa) Driver1NextDownloadDate = currentDate.AddDays(data[3]);
            }
            else {
                if (slot1 == CardPresent.DriverCard) {
                    // F99D - Get Driver1CardExpiryDate
                    data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.Driver1CardExpiryDate), true);
                    if (data.Length > 0) Driver1CardExpiryDate = ByteArray.ToFMSDate(data, 3);
                    // F99E - Get Driver1CardNextMandatoryDownloadDate
                    data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.Driver1CardNextMandatoryDownloadDate), true);
                    if (data.Length > 0) Driver1NextDownloadDate = ByteArray.ToFMSDate(data, 3);
                }
                if (slot2 == CardPresent.DriverCard) {
                    // F98B - Get Driver2CardExpiryDate
                    data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.Driver2CardExpiryDate), true);
                    if (data.Length > 0) Driver2CardExpiryDate = ByteArray.ToFMSDate(data, 3);
                    // F98C - Get Driver2CardNextMandatoryDownloadDate
                    data = await connection.ExecuteVUCommand(VehicleUnitProtocol.ReadDataByIdentifier(ReadDataIdentifier.Driver2CardNextMandatoryDownloadDate), true);
                    if (data.Length > 0) Driver2NextDownloadDate = ByteArray.ToFMSDate(data, 3);
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            if (slot1 > CardPresent.CardNotPresent) Console.WriteLine($"Slot 1: {slot1} : {slot1cardNumber} - {slot1DriverFirstnames} {slot1DriverSurname}");
            if (slot1 == CardPresent.DriverCard) Console.WriteLine($"Slot 1: Expiry Date: {Driver1CardExpiryDate:d} Next Download Date: {Driver1NextDownloadDate:d}");
            if (slot2 > CardPresent.CardNotPresent) Console.WriteLine($"Slot 2: {slot2} : {slot2cardNumber} - {slot2DriverFirstnames} {slot2DriverSurname}");
            if (slot2 == CardPresent.DriverCard) Console.WriteLine($"Slot 2: Expiry Date: {Driver2CardExpiryDate:d} Next Download Date: {Driver2NextDownloadDate:d}");

        }

        private static async Task Download()
        {
            await PrepareSerial();

            byte[] data = await DownloadTreps();

            string filename = "New download.ddd";
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), filename);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                var b = new BinaryWriter(fs);
                b.Write(data);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Request Transfer Exit");
            await connection.Send(VehicleUnitProtocol.RequestTransferExit());

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Stop Communications");
            await connection.Send(VehicleUnitProtocol.StopCommunications());
        }

        private static async Task PrepareSerial()
        {
            // Guard Time
            Thread.Sleep(500);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Prepare Serial");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Break into command mode");
            await connection.Send(new byte[] { 0x2b, 0x2b, 0x2b });

            // Guard Time
            Thread.Sleep(500);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Ask Acknowledge");
            await connection.ExecuteCommand(Protocol.AskAcknowledge());

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Switch to Serial");
            await connection.ExecuteCommand(Protocol.ConfigurePort(new byte[] { 0x56 }));

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Return to pass-through mode");
            await connection.ExecuteCommand(Protocol.ExitCommandMode(ByteArray.FromUInt16(BluetoothPIN)));

            int attempt = 0;
            while (true) {
                try {
                    attempt++;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Start Communications");
                    await connection.ExecuteVUCommand(VehicleUnitProtocol.StartCommunications());
                    break;
                }
                catch (TachographException ex) {
                    if (ex.ErrorCode != null && ex.ErrorCode[0] == 0xf0 && attempt < 5)
                        Thread.Sleep(2000);
                    else
                        throw;
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Start diagnostic");
            await connection.ExecuteVUCommand(VehicleUnitProtocol.StartDiagnostic());

            // Change baud rate
            bool success = false;
            try {
                await connection.ExecuteVUCommand(VehicleUnitProtocol.VerifyBaudRate((byte)0x05));
                success = true;
            }
            catch {
            }
            if (success) {
                await connection.Send(VehicleUnitProtocol.TransitionBaudRate());

                // Guard Time
                Thread.Sleep(500);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Break into command mode");
                await connection.Send(new byte[] { 0x2b, 0x2b, 0x2b });

                // Guard Time
                Thread.Sleep(500);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Ask Acknowledge");
                await connection.ExecuteCommand(Protocol.AskAcknowledge());

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Switch to faster baud rate");
                await connection.ExecuteCommand(Protocol.ConfigurePort(new byte[] { 0x53, 0x00, 0x01, 0xC2, 0x00, 0x01, 0x13, 0x88 }));

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Return to pass-through mode");
                await connection.ExecuteCommand(Protocol.ExitCommandMode(ByteArray.FromUInt16(BluetoothPIN)));
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Request Upload");
            await connection.ExecuteVUCommand(VehicleUnitProtocol.RequestUpload());
        }

        private static async Task<byte[]> DownloadTreps()
        {
            // Just requesting driver card download
            if (TREPS.Length == 1 && TREPS[0] == 0x06) {
                if (tachographGen2)
                    return await DownloadTrep(0x06, slotNumber);
                else
                    return await DownloadTrep(0x06);
            }

            var received = new ByteArray(0);

            var downloadInterfaceVersion = tachographGen2 ? "0100" : "0";
            // Before downloading Overview check interface version
            try {
                var buffer = await DownloadTrep(0x0);
                downloadInterfaceVersion = ByteArray.ToBCDString(buffer, 0, 2);

                received.Append(new byte[] { 0x76, 0x00 });
                received.Append(buffer);
            }
            catch (TachographException ex) {
                if (ex.ErrorCode == null || !ex.ErrorCode.SequenceEqual(new byte[] { 0x7f, 0x36, 0x12 }))
                    throw;
            }

            ApplyDownloadInterfaceVersionToTREPs(downloadInterfaceVersion);

            foreach (var trep in TREPS) {
                if (trep == 0x02 || trep == 0x22 || trep == 0x32) {
                    

                    var buffer = new byte[] { };
                    var data = new byte[] { };
                    TimeReal downloadDate = activityStartDate;
                    while (downloadDate <= activityEndDate) {
                        try {
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine(downloadDate.ToString("g"));
                            data = await DownloadTrep(trep, downloadDate);
                        }
                        catch (TachographException ex) {
                            if (ex.ErrorCode != null && (ex.ErrorCode.SequenceEqual(new byte[] { 0x7f, 0x36, 0x10 }) || ex.ErrorCode.SequenceEqual(new byte[] { 0x7f, 0x36, 0xfa })))
                                // No data for this day so continue
                                data = new byte[] { };
                            else if (ex.ErrorCode != null && ex.ErrorCode.SequenceEqual(new byte[] { 0x7f, 0x36, 0x22 })) {
                                // Conditions not correct - treat as data integrity error
                                await PrepareSerial();

                                buffer = new byte[] { };
                                continue;
                            }
                            else
                                throw;
                        }

                        // Save previous day
                        if (buffer.Length > 0) {
                            received.Append(new byte[] { 0x76, trep });
                            received.Append(buffer);
                        }

                        buffer = data;

                        downloadDate = downloadDate.AddMinutes(24 * 60);
                    }

                    // Save last day
                    if (data.Length > 0) {
                        received.Append(new byte[] { 0x76, trep });
                        received.Append(buffer);
                    }
                }
                else {
                    received.Append(new byte[] { 0x76, trep });
                    received.Append(await DownloadTrep(trep));
                }
            }

            return received.ToBytes();
        }

        private static async Task<byte[]> DownloadTrep(byte trep, TimeReal date)
        {
            return await DownloadTrep(trep, ByteArray.FromTimeReal(date));
        }
        private static async Task<byte[]> DownloadTrep(byte trep, byte slot)
        {
            return await DownloadTrep(trep, new byte[] { slot });
        }
        private static async Task<byte[]> DownloadTrep(byte trep)
        {
            return await DownloadTrep(trep, new byte[] { });
        }
        private static async Task<byte[]> DownloadTrep(byte trep, byte[] data)
        {
            var received = new ByteArray(0);
            byte[] buffer = await connection.ExecuteVUCommand(VehicleUnitProtocol.TransferDataRequest(trep, data));

            if (buffer.Length != 255)
                received.Append(ByteArray.Subbyte(buffer, 2, buffer.Length - 2));
            else {
                UInt16 msgC = 1;
                while (true) {
                    received.Append(ByteArray.Subbyte(buffer, 4, buffer.Length - 4));
                    if (buffer.Length != 255) break;

                    msgC++;
                    buffer = await connection.ExecuteVUCommand(VehicleUnitProtocol.AcknowledgeSubMessage(0x76, msgC));
                }
            }
            return received.ToBytes();
        }

        private static void ApplyDownloadInterfaceVersionToTREPs(string downloadInterfaceVersion)
        {
            for (int i = 0; i < TREPS.Length; i++) {
                if (downloadInterfaceVersion == "0100") {
                    switch (TREPS[i]) {
                        case 0x1: TREPS[i] = 0x21; break;
                        case 0x2: TREPS[i] = 0x22; break;
                        case 0x3: TREPS[i] = 0x23; break;
                        case 0x4: TREPS[i] = 0x24; break;
                        case 0x5: TREPS[i] = 0x25; break;
                    }
                }
                else if (downloadInterfaceVersion == "0101") {
                    switch (TREPS[i]) {
                        case 0x1: TREPS[i] = 0x31; break;
                        case 0x2: TREPS[i] = 0x32; break;
                        case 0x3: TREPS[i] = 0x33; break;
                        case 0x4: TREPS[i] = 0x24; break;
                        case 0x5: TREPS[i] = 0x35; break;
                    }
                }
            }
        }

        private static decimal ParseIntellicVersion(string version)
        {
            int dp = version.IndexOf(".");
            if (dp < 0)
                return 0;

            int major = int.Parse(version.Substring(0, dp));
            int minor = int.Parse(version.Substring(dp + 1));

            return major + (minor / 100m);
        }

        private static void LocateBluetoothDevice()
        {
            // List all available Bluetooth COM ports.
            string[] ports = BluetoothPortList("Serial over Bluetooth", "BTHENUM", "^DIGIBLU_[0-9]*");

            if (ports.Length > 0) {
                port = ports[0];

                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"Serial Bluetooth on port {port}");
            }
        }

        private static void StartBluetoothLEDiscovery()
        {
            // Notification for new BLE devices
            string aqsFilter = "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\"";
            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" };

            deviceWatcher = DeviceInformation.CreateWatcher(aqsFilter, requestedProperties, DeviceInformationKind.AssociationEndpoint);

            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.Updated += DeviceWatcher_Updated;

            deviceWatcher.Start();
        }

        private static void StopBluetoothLEDiscovery()
        {
            deviceWatcher.Stop();
            while (deviceWatcher.Status != DeviceWatcherStatus.Stopped) { Thread.Sleep(1); }

            deviceWatcher.Added -= DeviceWatcher_Added;
            deviceWatcher.Removed -= DeviceWatcher_Removed;
            deviceWatcher.Updated -= DeviceWatcher_Updated;
        }

        private static async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            if (args.Name != null && args.Name.Contains("DIGIBLULE")) {
                var cn = new BluetoothLEConnection(args.Id);
                if (await cn.Connect()) {
                    deviceId = args.Id;

                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"Bluetooth LE {args.Name}");
                    Console.ForegroundColor = ConsoleColor.White;
                }
                cn.Disconnect();
            }
        }
        private static void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            //throw new NotImplementedException();
        }
        private static void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            //throw new NotImplementedException();
        }

        /// <summary>
        /// Used to return list of Serial over Bluetooth devices
        /// </summary>
        /// <returns>Array of COM ports.</returns>
        private static string[] BluetoothPortList(string description, string deviceId, string name)
        {
            try {
                DeviceInterfaceSearcher dis = new DeviceInterfaceSearcher(description, deviceId);
                DeviceInterfaceCollection dic = dis.Get();

                List<string> ret = new List<string>();
                foreach (DeviceInterfacePath di in dic) {
                    if (Regex.IsMatch(di.DeviceDescription, name))
                        ret.Add(di.PortName);
                }
                return ret.ToArray();
            }
            catch {
                return new string[] { };
            }
        }
        
    }
}
