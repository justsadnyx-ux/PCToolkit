namespace PcToolkit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Management;
    using System.Runtime.InteropServices;
    using Microsoft.Win32;

    public sealed record DriveSlot(string Letter, string Label, long Total, long Free);

    internal static class SystemInfo
    {
        private static PerformanceCounter? _cpuCounter;
        private static bool _cpuCounterDead;

        static SystemInfo()
        {
            try
            {
                using var s = new ManagementObjectSearcher("select Name,NumberOfCores,NumberOfLogicalProcessors from Win32_Processor");
                foreach (var o in s.Get())
                {
                    var m = (ManagementObject)o;
                    CpuName = (m["Name"] as string)?.Replace("  ", " ").Trim() ?? "Unknown CPU";
                    CpuPhysicalCores = Convert.ToInt32(m["NumberOfCores"]);
                    CpuLogicalCores = Convert.ToInt32(m["NumberOfLogicalProcessors"]);
                    break;
                }
            }
            catch { }

            try
            {
                var names = new List<string>();
                using var s = new ManagementObjectSearcher("select Name from Win32_VideoController");
                foreach (var o in s.Get())
                {
                    if ((o as ManagementObject)?["Name"] is string n && !string.IsNullOrWhiteSpace(n))
                        names.Add(n.Trim());
                }
                if (names.Count > 0)
                    Gpu = string.Join(", ", names.Distinct());
            }
            catch { }

            try
            {
                ulong cap = 0;
                using var s = new ManagementObjectSearcher("select Capacity,Speed,ConfiguredClockSpeed from Win32_PhysicalMemory");
                foreach (var o in s.Get())
                {
                    var m = (ManagementObject)o;
                    if (m["Capacity"] != null)
                        cap += Convert.ToUInt64(m["Capacity"]);
                    if (RamSpeedMhz == 0)
                    {
                        int sp = m["Speed"] != null ? Convert.ToInt32(m["Speed"]) : 0;
                        int cc = m["ConfiguredClockSpeed"] != null ? Convert.ToInt32(m["ConfiguredClockSpeed"]) : 0;
                        RamSpeedMhz = sp > 0 ? sp : cc;
                    }
                }
                if (cap > 0)
                    RamTotalBytes = cap;
            }
            catch { }

            if (RamTotalBytes == 0)
                RamTotalBytes = MemSnapshot().Total;

            try
            {
                using var s = new ManagementObjectSearcher("select Manufacturer,Product from Win32_BaseBoard");
                foreach (var o in s.Get())
                {
                    var m = (ManagementObject)o;
                    Motherboard = $"{m["Manufacturer"]} {m["Product"]}".Trim();
                    break;
                }
            }
            catch { }

            OsLabel = BuildOsLabel();

            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _cpuCounter.NextValue();
            }
            catch
            {
                _cpuCounterDead = true;
            }
        }

        public static string CpuName { get; } = "Unknown CPU";
        public static int CpuPhysicalCores { get; }
        public static int CpuLogicalCores { get; } = Environment.ProcessorCount;
        public static string Gpu { get; } = "Unknown GPU";
        public static ulong RamTotalBytes { get; }
        public static int RamSpeedMhz { get; }
        public static string Motherboard { get; } = "";
        public static string OsLabel { get; }
        public static string MachineName => Environment.MachineName;
        public static string UserName => Environment.UserName;
        public static TimeSpan Uptime => TimeSpan.FromMilliseconds(Environment.TickCount64);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        public static (uint Percent, ulong Used, ulong Total) MemSnapshot()
        {
            var st = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref st))
                return (st.dwMemoryLoad, st.ullTotalPhys - st.ullAvailPhys, st.ullTotalPhys);
            return (0, 0, RamTotalBytes);
        }

        public static double GetCpuPercent()
        {
            if (!_cpuCounterDead && _cpuCounter != null)
            {
                try
                {
                    float v = _cpuCounter.NextValue();
                    return Math.Clamp(v, 0f, 100f);
                }
                catch
                {
                    _cpuCounterDead = true;
                }
            }

            try
            {
                using var s = new ManagementObjectSearcher("select LoadPercentage from Win32_Processor");
                foreach (var o in s.Get())
                {
                    var v = ((ManagementObject)o)["LoadPercentage"];
                    if (v != null)
                        return Math.Clamp(Convert.ToDouble(v), 0d, 100d);
                }
            }
            catch { }
            return 0;
        }

        public static List<DriveSlot> GetDrives()
        {
            var list = new List<DriveSlot>();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (d.DriveType != DriveType.Fixed || !d.IsReady || d.TotalSize <= 0)
                        continue;
                    string label;
                    try { label = d.VolumeLabel; } catch { label = ""; }
                    if (string.IsNullOrWhiteSpace(label))
                        label = "Local Disk";
                    list.Add(new DriveSlot(d.Name, label, d.TotalSize, d.AvailableFreeSpace));
                }
            }
            catch { }
            return list;
        }

        private static string BuildOsLabel()
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (k != null)
                {
                    string name = k.GetValue("ProductName") as string ?? "Windows";
                    string display = k.GetValue("DisplayVersion") as string ?? "";
                    string build = k.GetValue("CurrentBuildNumber") as string ?? "";
                    int ubr = k.GetValue("UBR") is int u ? u : 0;
                    if (name.Contains("Windows 10") && int.TryParse(build, out var b) && b >= 22000)
                        name = name.Replace("Windows 10", "Windows 11");

                    var parts = name;
                    if (display.Length > 0) parts += " " + display;
                    if (build.Length > 0) parts += $" (Build {build}.{ubr})";
                    return parts;
                }
            }
            catch { }
            return Environment.OSVersion.ToString();
        }
    }
}
