using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Tachosys.System;

namespace ProSys.Utilities
{

    sealed class DeviceInterfaceSearcher
    {

        const Int32 ERROR_SUCCESS = 0;
        const Int32 BUFFER_SIZE = 256;

        const UInt32 DIGCF_PRESENT = 0x2;
        const UInt32 DIGCF_ALLCLASSES = 0x4;
        const UInt32 DIGCF_DEVICEINTERFACE = 0x10;

        const UInt32 SPDRP_DEVICEDESC = 0x00000000;
        const UInt32 SPDRP_FRIENDLYNAME = 0x0000000C;

        const UInt32 DICS_FLAG_GLOBAL = 0x00000001;

        const UInt32 DIREG_DEV = 0x00000001;

        const UInt32 KEY_READ = 0x20019;

        IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVINFO_DATA
        {
            public UInt32 cbSize;
            public Guid ClassGuid;
            public UInt32 DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            internal UInt32 cbSize;
            internal System.Guid InterfaceClassGuid;
            internal UInt32 Flags;
            internal UIntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public UInt32 cbSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = BUFFER_SIZE)]
            public string DevicePath;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern int RegCloseKey(IntPtr hKey);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryValueExW", SetLastError = true)]
        static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, UInt32 lpReserved, out UInt32 lpType, IntPtr lpData, ref UInt32 lpcbData);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, UInt32 memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref System.Guid interfaceClassGuid, Int32 memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr SetupDiGetClassDevs(ref System.Guid classGuid, IntPtr enumerator, IntPtr hwndParent, UInt32 flags);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, IntPtr enumerator, IntPtr hwndParent, UInt32 flags);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, StringBuilder deviceInstanceId, UInt32 deviceInstanceIdSize, out UInt32 RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData, UInt32 deviceInterfaceDetailDataSize, out UInt32 requiredSize, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool SetupDiGetDeviceRegistryProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, UInt32 property, out UInt32 propertyRegDataType, IntPtr propertyBuffer, UInt32 propertyBufferSize, out UInt32 requiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr SetupDiOpenDevRegKey(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, UInt32 scope, UInt32 hwProfile, UInt32 keyType, UInt32 samDesired);

        public Guid Guid { get; private set; }
        public string DeviceDescriptionPattern { get; private set; }
        public string DeviceInstanceIdPattern { get; private set; }

        public DeviceInterfaceSearcher(Guid guid)
        {
            this.Guid = guid;
        }
        public DeviceInterfaceSearcher(string deviceDescriptionPattern, string deviceInstanceIdPattern)
        {
            this.DeviceDescriptionPattern = deviceDescriptionPattern;
            this.DeviceInstanceIdPattern = deviceInstanceIdPattern;
        }

        public DeviceInterfaceCollection Get()
        {
            if (this.Guid != Guid.Empty)
                return FindAllGuid();
            else if (!string.IsNullOrEmpty(this.DeviceDescriptionPattern) && !string.IsNullOrEmpty(DeviceInstanceIdPattern))
                return FindAllLike(this.DeviceDescriptionPattern, this.DeviceInstanceIdPattern);
            else
                return new DeviceInterfaceCollection();
        }

        private DeviceInterfaceCollection FindAllGuid()
        {
            DeviceInterfaceCollection collection = new DeviceInterfaceCollection();

            Guid guid = this.Guid;
            IntPtr deviceInfoSet = IntPtr.Zero;

            try {
                deviceInfoSet = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

                int index = 0;

                while (true) {
                    SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
                    deviceInterfaceData.cbSize = (UInt32)Marshal.SizeOf(deviceInterfaceData);

                    if (!SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref guid, index, ref deviceInterfaceData))
                        break;

                    SP_DEVINFO_DATA deviceInfoData = new SP_DEVINFO_DATA();
                    deviceInfoData.cbSize = (UInt32)Marshal.SizeOf(deviceInfoData);

                    SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData = new SP_DEVICE_INTERFACE_DETAIL_DATA();
                    if (IntPtr.Size == 8)
                        deviceInterfaceDetailData.cbSize = 8;
                    else
                        deviceInterfaceDetailData.cbSize = (UInt32)(IntPtr.Size + Marshal.SystemDefaultCharSize);

                    UInt32 requiredSize = 0;
                    UInt32 bytes = BUFFER_SIZE;
                    bool res = SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, ref deviceInterfaceDetailData, bytes, out requiredSize, ref deviceInfoData);
                    if (!res)
                        System.Diagnostics.Debug.WriteLine(Marshal.GetLastWin32Error().ToString());

                    collection.Add(new DeviceInterfacePath(deviceInterfaceDetailData.DevicePath));

                    index++;
                }
            }
            catch {
                throw;
            }
            finally {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return collection;
        }

        private DeviceInterfaceCollection FindAllLike(string descriptionPattern, string deviceInstanceIdPattern)
        {
            DeviceInterfaceCollection collection = new DeviceInterfaceCollection();

            IntPtr deviceInfoSet = IntPtr.Zero;
            IntPtr buffer = Marshal.AllocHGlobal(BUFFER_SIZE);

            try {
                deviceInfoSet = SetupDiGetClassDevs(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);

                UInt32 index = 0;

                while (true) {
                    SP_DEVINFO_DATA deviceInfoData = new SP_DEVINFO_DATA();
                    deviceInfoData.cbSize = (UInt32)Marshal.SizeOf(deviceInfoData);

                    if (!SetupDiEnumDeviceInfo(deviceInfoSet, index, ref deviceInfoData))
                        break;

                    UInt32 requiredSize = 0;
                    UInt32 propertyRegDataType = 0;

                    StringBuilder sb = new StringBuilder(BUFFER_SIZE);
                    if (SetupDiGetDeviceInstanceId(deviceInfoSet, ref deviceInfoData, sb, BUFFER_SIZE, out requiredSize)) {
                        string deviceInstanceId = sb.ToString();

                        if (!string.IsNullOrEmpty(deviceInstanceId) && Regex.IsMatch(deviceInstanceId, deviceInstanceIdPattern)) {
                            string deviceDescription = null;
                            if (SetupDiGetDeviceRegistryProperty(deviceInfoSet, ref deviceInfoData, SPDRP_FRIENDLYNAME, out propertyRegDataType, buffer, BUFFER_SIZE, out requiredSize))
                                deviceDescription = Marshal.PtrToStringAuto(buffer);

                            if (!string.IsNullOrEmpty(deviceDescription) && Regex.IsMatch(deviceDescription, descriptionPattern)) {

                                string bluetoothName = GetBluetoothRegistryName(deviceInstanceId);

                                string portName = QueryDevRegKey(deviceInfoSet, deviceInfoData, "PortName");
                                if (!string.IsNullOrEmpty(portName))
                                    collection.Add(new DeviceInterfacePath(bluetoothName ?? deviceDescription, portName));
                            }
                        }
                    }
                    index++;
                }
            }
            catch {
                throw;
            }
            finally {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
                Marshal.FreeHGlobal(buffer);
            }
            return collection;
        }

        private string QueryDevRegKey(IntPtr deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, string valueName)
        {
            IntPtr handleDeviceRegKey = SetupDiOpenDevRegKey(deviceInfoSet, ref deviceInfoData, DICS_FLAG_GLOBAL, 0, DIREG_DEV, KEY_READ);
            if (handleDeviceRegKey == INVALID_HANDLE_VALUE)
                return null;

            UInt32 type = 0;
            IntPtr buffer = Marshal.AllocHGlobal(BUFFER_SIZE);
            UInt32 size = BUFFER_SIZE;

            try {
                int result = RegQueryValueEx(handleDeviceRegKey, valueName, 0, out type, buffer, ref size);
                if (result == ERROR_SUCCESS)
                    return Marshal.PtrToStringAuto(buffer);
            }
            catch {
                throw;
            }
            finally {
                RegCloseKey(handleDeviceRegKey);
                Marshal.FreeHGlobal(buffer);
            }
            return null;
        }

        private string GetBluetoothRegistryName(string deviceInstanceId)
        {
            try {
                string[] tokens = deviceInstanceId.Split('&');
                string[] addressToken = tokens[4].Split('_');
                string bluetoothAddress = addressToken[0];

                string registryPath = @"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices";
                string devicePath = String.Format(@"{0}\{1}", registryPath, bluetoothAddress);

                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(devicePath)) {
                    if (key != null) {
                        byte[] buffer = (byte[])key.GetValue("Name");
                        return ByteArray.ToIA5String(buffer, 0, buffer.Length);
                    }
                }
            }
            catch { }
            return null;
        }
    }

    sealed class DeviceInterfaceCollection : List<DeviceInterfacePath>
    {

    }

    sealed class DeviceInterfacePath
    {

        public DeviceInterfacePath(string path)
        {
            this.DevicePath = path;
        }
        public DeviceInterfacePath(string deviceDescription, string portName)
        {
            this.DeviceDescription = deviceDescription;
            this.PortName = portName;
        }

        public string DevicePath { get; private set; }
        public string DeviceDescription { get; private set; }
        public string PortName { get; private set; }

    }

}
