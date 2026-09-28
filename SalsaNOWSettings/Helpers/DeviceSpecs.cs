using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace SalsaNOWSettings.Helpers;

public sealed class DeviceSpecs
{
    public string DeviceName { get; init; } = Environment.MachineName;
    public string Model { get; init; } = string.Empty;
    public string Processor { get; init; } = string.Empty;
    public string ProcessorSpeed { get; init; } = string.Empty;
    public string ProcessorCores { get; init; } = string.Empty;
    public string InstalledRam { get; init; } = string.Empty;
    public string UsableRam { get; init; } = string.Empty;
    public string Graphics { get; init; } = string.Empty;
    public string GraphicsMemory { get; init; } = string.Empty;
    public string StorageTotal { get; init; } = string.Empty;
    public string StorageUsed { get; init; } = string.Empty;
    public bool HasPersistentStorage { get; init; } = true;
    public string DeviceId { get; init; } = string.Empty;
    public string ProductId { get; init; } = string.Empty;
    public string SystemType { get; init; } = string.Empty;
    public string Edition { get; init; } = string.Empty;
    public string OsBuild { get; init; } = string.Empty;
    public string PenAndTouch { get; init; } = "No pen or touch input is available for this display";

    public static DeviceSpecs Load()
    {
        var memory = ReadMemory();
        var storage = ReadStorage();
        var processor = ReadRegistry(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") ?? "Unknown";
        var mhz = ReadRegistry(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz");
        var speed = int.TryParse(mhz, out var mhzValue) && mhzValue > 0
            ? $"{mhzValue / 1000.0:0.00} GHz"
            : string.Empty;
        var graphics = ReadGraphics();
        var windows = ReadWindowsIdentity();
        var ram = FormatRam(memory.Total);

        return new DeviceSpecs
        {
            DeviceName = Environment.MachineName,
            Model = ReadRegistry(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemProductName")
                    ?? ReadRegistry(@"SYSTEM\CurrentControlSet\Control\SystemInformation", "SystemProductName")
                    ?? string.Empty,
            Processor = processor.Trim(),
            ProcessorSpeed = speed,
            ProcessorCores = FormatCores(ReadProcessorCounts()),
            InstalledRam = ram.Installed,
            UsableRam = ram.Usable,
            Graphics = graphics.Name,
            GraphicsMemory = graphics.Memory,
            StorageTotal = storage.Total == 0 ? "Unavailable" : FormatBytes(storage.Total),
            StorageUsed = storage.Total == 0
                ? "I: is not available"
                : $"{FormatBytes(storage.Used)} of {FormatBytes(storage.Total)} used",
            HasPersistentStorage = storage.Persistent,
            DeviceId = ReadRegistry(@"SOFTWARE\Microsoft\SQMClient", "MachineId")?.Trim('{', '}')
                       ?? ReadRegistry(@"SOFTWARE\Microsoft\Cryptography", "MachineGuid")
                       ?? string.Empty,
            ProductId = ReadRegistry(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductId") ?? string.Empty,
            SystemType = Environment.Is64BitOperatingSystem
                ? "64-bit operating system, x64-based processor"
                : "32-bit operating system",
            Edition = windows.Edition,
            OsBuild = windows.Build,
            PenAndTouch = ReadPenAndTouch()
        };
    }

    public string ToCopyText()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Device name:\t{DeviceName}");
        builder.AppendLine($"Processor:\t{FormatProcessorLine()}");
        builder.AppendLine($"Installed RAM:\t{FormatRamLine()}");
        builder.AppendLine($"Edition:\t{Edition}");
        builder.AppendLine($"OS build:\t{OsBuild}");
        if (!string.IsNullOrEmpty(Graphics))
        {
            builder.AppendLine($"Graphics:\t{Graphics}" + (string.IsNullOrEmpty(GraphicsMemory) ? string.Empty : $"  ({GraphicsMemory})"));
        }
        builder.AppendLine($"Device ID:\t{DeviceId}");
        builder.AppendLine($"Product ID:\t{ProductId}");
        builder.AppendLine($"System type:\t{SystemType}");
        builder.AppendLine($"Pen and touch:\t{PenAndTouch}");
        return builder.ToString();
    }

    public string FormatProcessorLine()
    {
        var details = JoinDetails(ProcessorSpeed, ProcessorCores);
        return string.IsNullOrEmpty(details) ? Processor : $"{Processor}  ({details})";
    }

    public string FormatProcessorDetails() => JoinDetails(ProcessorSpeed, ProcessorCores);

    public string FormatProcessorSpeedLabel() =>
        string.IsNullOrEmpty(ProcessorSpeed) ? string.Empty : $"({ProcessorSpeed})";

    public string FormatUsableRamLabel() =>
        string.IsNullOrEmpty(UsableRam) ? string.Empty : $"({UsableRam} usable)";

    public string FormatRamLine() =>
        string.IsNullOrEmpty(UsableRam) ? InstalledRam : $"{InstalledRam} ({UsableRam} usable)";

    private static string JoinDetails(params string[] parts) =>
        string.Join("  ·  ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

    private static (string Installed, string Usable) FormatRam(ulong usableBytes)
    {
        var installedBytes = usableBytes;
        if (GetPhysicallyInstalledSystemMemory(out var kilobytes) && kilobytes > 0)
        {
            installedBytes = kilobytes * 1024;
        }

        var installed = FormatBytes(installedBytes);
        var usable = FormatBytes(usableBytes);
        return string.Equals(installed, usable, StringComparison.Ordinal)
            ? (installed, string.Empty)
            : (installed, usable);
    }

    private static (string Edition, string Build) ReadWindowsIdentity()
    {
        const string path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        var build = ReadRegistry(path, "CurrentBuild")
                    ?? ReadRegistry(path, "CurrentBuildNumber")
                    ?? string.Empty;
        var revision = ReadRegistry(path, "UBR");
        return (
            ReadBranding() ?? ReadEditionFallback(path, build),
            string.IsNullOrEmpty(revision) ? build : $"{build}.{revision}");
    }

    private static string? ReadBranding()
    {
        try
        {
            var pointer = BrandingFormatString("%WINDOWS_LONG%");
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var value = Marshal.PtrToStringUni(pointer);
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
            finally
            {
                LocalFree(pointer);
            }
        }
        catch
        {
            return null;
        }
    }

    private static string ReadEditionFallback(string path, string buildText)
    {
        var productName = ReadRegistry(path, "ProductName") ?? "Windows";
        var editionId = ReadRegistry(path, "EditionID") ?? string.Empty;
        if (int.TryParse(buildText, out var build) && build >= 22000)
        {
            productName = productName.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase);
        }

        if (IsLtsc(editionId, productName) &&
            !productName.Contains("LTSC", StringComparison.OrdinalIgnoreCase) &&
            !productName.Contains("LTSB", StringComparison.OrdinalIgnoreCase))
        {
            productName = $"{productName.TrimEnd()} LTSC";
        }

        return productName.Trim();
    }

    private static bool IsLtsc(string editionId, string productName) =>
        ContainsAny(editionId, "EnterpriseS", "EnterpriseSN", "IoTEnterpriseS", "IoTEnterpriseSK", "LTSC", "LTSB") ||
        ContainsAny(productName, "LTSC", "LTSB");

    private static (ulong Total, ulong Available) ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status)
            ? (status.TotalPhys, status.AvailPhys)
            : (0, 0);
    }

    private static (ulong Total, ulong Used, bool Persistent) ReadStorage()
    {
        try
        {
            var drive = new DriveInfo(DriveUsage.DriveLetter);
            if (!drive.IsReady)
            {
                return (0, 0, false);
            }

            var total = (ulong)drive.TotalSize;
            var free = (ulong)drive.TotalFreeSpace;
            const ulong ephemeralLimit = 110UL * 1024UL * 1024UL * 1024UL;
            return (total, total > free ? total - free : 0, total > ephemeralLimit);
        }
        catch
        {
            return (0, 0, false);
        }
    }

    private static (string Name, string Memory) ReadGraphics()
    {
        var live = ReadDxgiAdapters();
        var attached = ReadActiveDisplayNames();
        var chosen = live
            .OrderBy(adapter => attached.Count == 0 || attached.Any(name => NamesMatch(name, adapter.Name)) ? 0 : 1)
            .ThenBy(adapter => adapter.HasOutput ? 0 : 1)
            .ThenBy(adapter => AdapterPriority(adapter.Name))
            .ThenByDescending(adapter => adapter.Bytes)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(chosen.Name) && attached.Count > 0)
        {
            var name = attached.OrderBy(AdapterPriority).First();
            return (name, string.Empty);
        }

        if (!string.IsNullOrEmpty(chosen.Name))
        {
            return (chosen.Name, chosen.Bytes > 0 ? FormatBytes(chosen.Bytes) : string.Empty);
        }

        var fallback = ReadDirectXAdapters()
            .OrderBy(item => item.Priority)
            .ThenByDescending(item => item.Bytes)
            .FirstOrDefault();
        return string.IsNullOrEmpty(fallback.Name)
            ? ("Unknown", string.Empty)
            : (fallback.Name, fallback.Bytes > 0 ? FormatBytes(fallback.Bytes) : string.Empty);
    }

    private static List<(string Name, ulong Bytes, bool HasOutput)> ReadDxgiAdapters()
    {
        var adapters = new List<(string Name, ulong Bytes, bool HasOutput)>();
        var iid = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
        if (CreateDXGIFactory1(ref iid, out var factory) < 0 || factory is null)
        {
            return adapters;
        }

        try
        {
            for (uint index = 0; factory.EnumAdapters(index, out var adapter) >= 0; index++)
            {
                try
                {
                    if (adapter.GetDesc(out var desc) < 0 ||
                        desc.VendorId == 0x1414 ||
                        string.IsNullOrWhiteSpace(desc.Description) ||
                        IsIgnoredAdapter(desc.Description))
                    {
                        continue;
                    }

                    var hasOutput = adapter.EnumOutputs(0, out var output) >= 0;
                    if (hasOutput && output != IntPtr.Zero)
                    {
                        Marshal.Release(output);
                    }

                    adapters.Add((desc.Description.Trim(), desc.DedicatedVideoMemory.ToUInt64(), hasOutput));
                }
                finally
                {
                    Marshal.ReleaseComObject(adapter);
                }
            }
        }
        catch
        {
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }

        return adapters;
    }

    private static List<string> ReadActiveDisplayNames()
    {
        var names = new List<string>();
        var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
        for (uint index = 0; EnumDisplayDevices(null, index, ref device, 0); index++)
        {
            const uint active = 1;
            const uint mirroring = 8;
            if ((device.StateFlags & mirroring) != 0 ||
                (device.StateFlags & active) == 0 ||
                string.IsNullOrWhiteSpace(device.DeviceString) ||
                IsIgnoredAdapter(device.DeviceString))
            {
                device.Size = Marshal.SizeOf<DisplayDevice>();
                continue;
            }

            var name = device.DeviceString.Trim();
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(name);
            }

            device.Size = Marshal.SizeOf<DisplayDevice>();
        }

        return names;
    }

    private static bool NamesMatch(string left, string right) =>
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static List<(string Name, ulong Bytes, int Priority)> ReadDirectXAdapters()
    {
        var adapters = new List<(string Name, ulong Bytes, int Priority)>();
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\DirectX");
            if (key is null)
            {
                return adapters;
            }

            foreach (var name in key.GetSubKeyNames())
            {
                using var adapter = key.OpenSubKey(name);
                var description = adapter?.GetValue("Description") as string;
                if (string.IsNullOrWhiteSpace(description) || IsIgnoredAdapter(description))
                {
                    continue;
                }

                TryReadBytes(adapter?.GetValue("DedicatedVideoMemory"), out var bytes);
                adapters.Add((description, bytes, AdapterPriority(description)));
            }
        }
        catch
        {
        }

        return adapters;
    }

    private static int AdapterPriority(string description)
    {
        if (ContainsAny(description, "NVIDIA", "GeForce", "Quadro", "RTX", "GTX"))
        {
            return 0;
        }

        if (ContainsAny(description, "AMD", "Radeon"))
        {
            return 1;
        }

        if (ContainsAny(description, "Intel", "Arc"))
        {
            return 2;
        }

        return 3;
    }

    private static bool IsIgnoredAdapter(string description) =>
        ContainsAny(
            description,
            "Microsoft Basic",
            "Microsoft Basic Render",
            "Remote Desktop",
            "WARP",
            "Red Hat",
            "QXL",
            "Virtio",
            "VirtIO",
            "VMware",
            "VirtualBox",
            "Hyper-V",
            "Citrix",
            "Parallels",
            "SPICE",
            "Indirect Display");

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static bool TryReadBytes(object? value, out ulong bytes)
    {
        switch (value)
        {
            case long signed when signed > 0:
                bytes = (ulong)signed;
                return true;
            case ulong unsigned when unsigned > 0:
                bytes = unsigned;
                return true;
            case int signed32 when signed32 > 0:
                bytes = (ulong)signed32;
                return true;
            case uint unsigned32 when unsigned32 > 0:
                bytes = unsigned32;
                return true;
            case byte[] raw when raw.Length == 8:
                bytes = BitConverter.ToUInt64(raw, 0);
                return bytes > 0;
            case byte[] raw when raw.Length == 4:
                bytes = BitConverter.ToUInt32(raw, 0);
                return bytes > 0;
            default:
                bytes = 0;
                return false;
        }
    }

    private static (int Physical, int Logical) ReadProcessorCounts()
    {
        var logical = Math.Max(Environment.ProcessorCount, 1);
        var physical = CountPhysicalCores();
        return (physical > 0 ? physical : logical, logical);
    }

    private static int CountPhysicalCores()
    {
        uint length = 0;
        GetLogicalProcessorInformation(IntPtr.Zero, ref length);
        if (length == 0)
        {
            return 0;
        }

        var buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (!GetLogicalProcessorInformation(buffer, ref length))
            {
                return 0;
            }

            var size = Marshal.SizeOf<SystemLogicalProcessorInformation>();
            var count = (int)length / size;
            var cores = 0;
            var pointer = buffer;
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<SystemLogicalProcessorInformation>(pointer);
                if (info.Relationship == 0)
                {
                    cores++;
                }

                pointer += size;
            }

            return cores;
        }
        catch
        {
            return 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string FormatCores((int Physical, int Logical) counts)
    {
        if (counts.Physical <= 0)
        {
            return string.Empty;
        }

        var cores = counts.Physical == 1 ? "1 core" : $"{counts.Physical} cores";
        return counts.Logical > counts.Physical
            ? $"{cores} ({counts.Logical} logical)"
            : cores;
    }

    private static string ReadPenAndTouch()
    {
        var touches = GetSystemMetrics(95);
        return touches > 0
            ? $"{touches} touch points available for this display"
            : "No pen or touch input is available for this display";
    }

    private static string? ReadRegistry(string path, string name)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return key?.GetValue(name)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string FormatBytes(ulong bytes)
    {
        const double kb = 1024;
        const double mb = kb * 1024;
        const double gb = mb * 1024;
        const double tb = gb * 1024;
        if (bytes >= tb)
        {
            return $"{bytes / tb:0.00} TB";
        }

        if (bytes >= gb)
        {
            return $"{bytes / gb:0.0} GB";
        }

        return $"{bytes / mb:0} MB";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemLogicalProcessorInformation
    {
        public UIntPtr ProcessorMask;
        public int Relationship;
        public ulong Reserved0;
        public ulong Reserved1;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr handle);

    [DllImport("winbrand.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr BrandingFormatString(string format);

    [DllImport("kernel32.dll")]
    private static extern bool GetLogicalProcessorInformation(IntPtr buffer, ref uint returnedLength);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? device, uint deviceIndex, ref DisplayDevice displayDevice, uint flags);

    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1(ref Guid riid, out IDXGIFactory1 factory);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DxgiAdapterDesc
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public long AdapterLuid;
    }

    [ComImport]
    [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIFactory1
    {
        int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);
        int SetPrivateDataInterface(ref Guid name, [MarshalAs(UnmanagedType.IUnknown)] object unknown);
        int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);
        int GetParent(ref Guid riid, out IntPtr parent);
        [PreserveSig]
        int EnumAdapters(uint adapter, out IDXGIAdapter result);
        int MakeWindowAssociation(IntPtr windowHandle, uint flags);
        int GetWindowAssociation(out IntPtr windowHandle);
        int CreateSwapChain([MarshalAs(UnmanagedType.IUnknown)] object device, IntPtr description, out IntPtr swapChain);
        int CreateSoftwareAdapter(IntPtr module, out IntPtr adapter);
        [PreserveSig]
        int EnumAdapters1(uint adapter, out IntPtr result);
        [PreserveSig]
        int IsCurrent();
    }

    [ComImport]
    [Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter
    {
        int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);
        int SetPrivateDataInterface(ref Guid name, [MarshalAs(UnmanagedType.IUnknown)] object unknown);
        int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);
        int GetParent(ref Guid riid, out IntPtr parent);
        [PreserveSig]
        int EnumOutputs(uint output, out IntPtr result);
        [PreserveSig]
        int GetDesc(out DxgiAdapterDesc description);
        int CheckInterfaceSupport(ref Guid name, out long version);
    }
}
