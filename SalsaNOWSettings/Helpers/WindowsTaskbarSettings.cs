using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace SalsaNOWSettings.Helpers;

public static class WindowsTaskbarSettings
{
    private const string AdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string StuckRectsKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3";
    private static readonly object Gate = new();

    public static int GetAlignment() => ReadDword("TaskbarAl", 1);
    public static void SetAlignment(int value)
    {
        var alignment = value is 0 or 1 ? value : 1;
        Persist(config => config.TaskbarAlignment = alignment == 0 ? "left" : "center");
        WriteDword("TaskbarAl", alignment);
    }

    public static bool GetAutoHide()
    {
        var data = NewAppBarData();
        return (SHAppBarMessage(4, ref data) & 1) != 0;
    }

    public static void SetAutoHide(bool hide)
    {
        Persist(config => config.TaskbarAutoHide = hide);
        QueueApply(() =>
        {
            var data = NewAppBarData();
            var current = SHAppBarMessage(4, ref data);
            data.lParam = (nint)((current & 2) | (hide ? 1u : 0u));
            SHAppBarMessage(10, ref data);
            WriteStuckRectsAutoHide(hide);
            NotifyExplorer();
        });
    }

    public static bool GetBadges() => ReadDword("TaskbarBadges", 1) != 0;
    public static void SetBadges(bool value)
    {
        Persist(config => config.TaskbarBadges = value);
        WriteDword("TaskbarBadges", value ? 1 : 0);
    }

    public static bool GetFlashing() => ReadDword("TaskbarFlashing", 1) != 0;
    public static void SetFlashing(bool value)
    {
        Persist(config => config.TaskbarFlashing = value);
        WriteDword("TaskbarFlashing", value ? 1 : 0);
    }

    public static bool GetShareWindows() => ReadDword("TaskbarSn", 1) != 0;
    public static void SetShareWindows(bool value)
    {
        Persist(config => config.TaskbarShareWindows = value);
        WriteDword("TaskbarSn", value ? 1 : 0);
    }

    public static bool GetShowDesktopCorner() => ReadDword("TaskbarSd", 1) != 0;
    public static void SetShowDesktopCorner(bool value)
    {
        Persist(config => config.TaskbarShowDesktopCorner = value);
        WriteDword("TaskbarSd", value ? 1 : 0);
    }

    public static int GetCombine() => ReadDword("TaskbarGlomLevel", 0);
    public static void SetCombine(int value)
    {
        var combine = value is >= 0 and <= 2 ? value : 0;
        Persist(config => config.TaskbarCombine = SalsaNOWConfig.CombineName(combine));
        WriteDword("TaskbarGlomLevel", combine);
    }

    public static int GetSmallerButtons()
    {
        var preference = ReadDword("IconSizePreference", -1);
        if (preference is >= 0 and <= 2)
        {
            return preference;
        }

        return ReadDword("TaskbarSi", 1) == 0 ? 0 : 1;
    }

    public static void SetSmallerButtons(int value)
    {
        var preference = value is >= 0 and <= 2 ? value : 1;
        Persist(config => config.TaskbarSmallerButtons = SalsaNOWConfig.SmallerName(preference));
        QueueApply(() =>
        {
            WriteDwordCore("IconSizePreference", preference);
            if (preference == 0)
            {
                WriteDwordCore("TaskbarSi", 0);
            }

            NotifyExplorer();
        });
    }

    public static void ApplyFromConfig()
    {
        var config = SalsaNOWConfig.Current;
        SetAlignment(config.TaskbarAlignment == "left" ? 0 : 1);
        if (config.TaskbarAutoHide is bool autoHide)
        {
            SetAutoHide(autoHide);
        }

        if (config.TaskbarBadges is bool badges)
        {
            SetBadges(badges);
        }

        if (config.TaskbarFlashing is bool flashing)
        {
            SetFlashing(flashing);
        }

        if (config.TaskbarShareWindows is bool share)
        {
            SetShareWindows(share);
        }

        if (config.TaskbarShowDesktopCorner is bool showDesktop)
        {
            SetShowDesktopCorner(showDesktop);
        }

        SetCombine(SalsaNOWConfig.CombineValue(config.TaskbarCombine));
        SetSmallerButtons(SalsaNOWConfig.SmallerValue(config.TaskbarSmallerButtons));
        TranslucentTb.ApplyFromConfig();
    }

    private static void Persist(Action<SalsaNOWConfigData> change)
    {
        try
        {
            SalsaNOWConfig.Update(change);
        }
        catch
        {
        }
    }

    private static void WriteStuckRectsAutoHide(bool hide)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StuckRectsKey, true);
            if (key?.GetValue("Settings") is not byte[] settings || settings.Length < 9)
            {
                return;
            }

            settings[8] = hide
                ? (byte)(settings[8] | 0x01)
                : (byte)(settings[8] & ~0x01);
            key.SetValue("Settings", settings, RegistryValueKind.Binary);
        }
        catch
        {
        }
    }

    private static int ReadDword(string name, int fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AdvancedKey);
            return key?.GetValue(name) is int value ? value : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static void WriteDword(string name, int value) =>
        QueueApply(() =>
        {
            WriteDwordCore(name, value);
            NotifyExplorer();
        });

    private static void WriteDwordCore(string name, int value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(AdvancedKey);
            key?.SetValue(name, value, RegistryValueKind.DWord);
        }
        catch
        {
        }
    }

    private static void QueueApply(Action apply) =>
        ThreadPool.QueueUserWorkItem(_ =>
        {
            lock (Gate)
            {
                apply();
            }
        });

    private static void NotifyExplorer()
    {
        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray != nint.Zero)
        {
            SendNotifyMessage(tray, 0x001A, nint.Zero, "TraySettings");
        }

        SendNotifyMessage((nint)0xFFFF, 0x001A, nint.Zero, "TraySettings");
    }

    private static AppBarData NewAppBarData()
    {
        var data = new AppBarData
        {
            cbSize = Marshal.SizeOf<AppBarData>(),
            hWnd = FindWindow("Shell_TrayWnd", null)
        };
        return data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public int cbSize;
        public nint hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public int left;
        public int top;
        public int right;
        public int bottom;
        public nint lParam;
    }

    [DllImport("shell32.dll")]
    private static extern uint SHAppBarMessage(uint message, ref AppBarData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SendNotifyMessage(nint hWnd, uint msg, nint wParam, string lParam);
}
