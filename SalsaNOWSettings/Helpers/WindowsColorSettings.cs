using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace SalsaNOWSettings.Helpers;

public enum ColorMode
{
    Light,
    Dark,
    Custom
}

public static class WindowsColorSettings
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string DwmKey = @"Software\Microsoft\Windows\DWM";
    private const string AccentKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    private const string DesktopKey = @"Control Panel\Desktop";
    private const string RecentSetting = "RecentAccentColors";
    private const string AccentSourceSetting = "AccentSourceAutomatic";

    public static readonly Color[] WindowsColors =
    [
        Color.FromArgb(255, 255, 185, 0),
        Color.FromArgb(255, 255, 140, 0),
        Color.FromArgb(255, 247, 99, 12),
        Color.FromArgb(255, 202, 80, 16),
        Color.FromArgb(255, 218, 59, 1),
        Color.FromArgb(255, 239, 105, 80),
        Color.FromArgb(255, 209, 52, 56),
        Color.FromArgb(255, 255, 67, 67),
        Color.FromArgb(255, 231, 72, 86),
        Color.FromArgb(255, 232, 17, 35),
        Color.FromArgb(255, 234, 0, 94),
        Color.FromArgb(255, 195, 0, 82),
        Color.FromArgb(255, 227, 0, 140),
        Color.FromArgb(255, 191, 0, 119),
        Color.FromArgb(255, 194, 57, 179),
        Color.FromArgb(255, 154, 0, 137),
        Color.FromArgb(255, 0, 120, 212),
        Color.FromArgb(255, 0, 99, 177),
        Color.FromArgb(255, 142, 140, 216),
        Color.FromArgb(255, 107, 105, 214),
        Color.FromArgb(255, 135, 100, 184),
        Color.FromArgb(255, 116, 77, 169),
        Color.FromArgb(255, 177, 70, 194),
        Color.FromArgb(255, 136, 23, 152),
        Color.FromArgb(255, 0, 153, 188),
        Color.FromArgb(255, 45, 125, 154),
        Color.FromArgb(255, 0, 183, 195),
        Color.FromArgb(255, 3, 131, 135),
        Color.FromArgb(255, 0, 178, 148),
        Color.FromArgb(255, 1, 133, 116),
        Color.FromArgb(255, 0, 204, 106),
        Color.FromArgb(255, 16, 137, 62),
        Color.FromArgb(255, 122, 117, 116),
        Color.FromArgb(255, 93, 90, 88),
        Color.FromArgb(255, 104, 118, 138),
        Color.FromArgb(255, 81, 92, 107),
        Color.FromArgb(255, 86, 124, 115),
        Color.FromArgb(255, 72, 104, 96),
        Color.FromArgb(255, 73, 130, 5),
        Color.FromArgb(255, 16, 124, 16),
        Color.FromArgb(255, 118, 118, 118),
        Color.FromArgb(255, 76, 74, 72),
        Color.FromArgb(255, 105, 121, 126),
        Color.FromArgb(255, 74, 84, 89),
        Color.FromArgb(255, 100, 124, 100),
        Color.FromArgb(255, 82, 94, 84),
        Color.FromArgb(255, 132, 117, 69),
        Color.FromArgb(255, 126, 115, 95),
    ];

    public static readonly Color DefaultAccent = Color.FromArgb(255, 76, 194, 255);

    public static ColorMode GetMode()
    {
        var appsLight = ReadInt(PersonalizeKey, "AppsUseLightTheme", 1) == 1;
        var systemLight = ReadInt(PersonalizeKey, "SystemUsesLightTheme", 1) == 1;
        if (appsLight && systemLight)
        {
            return ColorMode.Light;
        }

        if (!appsLight && !systemLight)
        {
            return ColorMode.Dark;
        }

        return ColorMode.Custom;
    }

    public static bool GetAppsUseLightTheme() => ReadInt(PersonalizeKey, "AppsUseLightTheme", 1) == 1;

    public static bool GetSystemUsesLightTheme() => ReadInt(PersonalizeKey, "SystemUsesLightTheme", 1) == 1;

    public static void SetMode(ColorMode mode)
    {
        if (mode == ColorMode.Custom)
        {
            ApplyAppTheme();
            PersistColors(ColorMode.Custom);
            return;
        }

        var light = mode == ColorMode.Light ? 1 : 0;
        WriteInt(PersonalizeKey, "AppsUseLightTheme", light);
        WriteInt(PersonalizeKey, "SystemUsesLightTheme", light);
        BroadcastColorChange();
        ApplyAppTheme();
        PersistColors(mode);
    }

    public static void SetWindowsMode(bool light)
    {
        WriteInt(PersonalizeKey, "SystemUsesLightTheme", light ? 1 : 0);
        BroadcastColorChange();
        ApplyAppTheme();
        PersistColors();
    }

    public static void SetAppMode(bool light)
    {
        WriteInt(PersonalizeKey, "AppsUseLightTheme", light ? 1 : 0);
        BroadcastColorChange();
        ApplyAppTheme();
        PersistColors();
    }

    public static bool GetTransparency() => ReadInt(PersonalizeKey, "EnableTransparency", 1) == 1;

    public static void SetTransparency(bool enabled)
    {
        WriteInt(PersonalizeKey, "EnableTransparency", enabled ? 1 : 0);
        BroadcastColorChange();
        PersistColors();
    }

    public static bool GetAutomaticAccent()
    {
        try
        {
            SalsaNOWConfig.EnsureExists();
            return SalsaNOWConfig.Current.AccentSourceAutomatic;
        }
        catch
        {
        }

        try
        {
            if (ApplicationData.Current.LocalSettings.Values[AccentSourceSetting] is bool automatic)
            {
                return automatic;
            }
        }
        catch
        {
        }

        return false;
    }

    public static void SetAutomaticAccent(bool enabled)
    {
        SaveAutomatic(enabled);
        if (enabled)
        {
            ApplyColor(DefaultAccent);
            PersistColors();
            return;
        }

        WriteInt(DesktopKey, "AutoColorization", 0);
        BroadcastColorChange();
        PersistColors();
    }

    public static Color GetAccent()
    {
        try
        {
            var color = new UISettings().GetColorValue(UIColorType.Accent);
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }
        catch
        {
            var value = (uint)ReadInt(DwmKey, "AccentColor", unchecked((int)0xFFD47800));
            return Color.FromArgb(255, (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF), (byte)((value >> 16) & 0xFF));
        }
    }

    public static void SetAccent(Color color)
    {
        SaveAutomatic(false);
        ApplyColor(color);
        RememberRecent(color);
        PersistColors();
    }

    public static void ApplyFromConfig()
    {
        var config = SalsaNOWConfig.Current;
        switch (config.ColorMode)
        {
            case "light":
                SetMode(ColorMode.Light);
                break;
            case "custom":
                SetWindowsMode(config.WindowsMode != "dark");
                SetAppMode(config.AppsMode != "dark");
                break;
            default:
                SetMode(ColorMode.Dark);
                break;
        }

        if (config.Transparency is bool transparency)
        {
            SetTransparency(transparency);
        }

        if (config.AccentSourceAutomatic)
        {
            SetAutomaticAccent(true);
            return;
        }

        if (TryParseHex(config.AccentColor ?? string.Empty) is Color accent)
        {
            SetAccent(accent);
            return;
        }

        SetAutomaticAccent(false);
    }

    private static void ApplyColor(Color color)
    {
        WriteInt(DesktopKey, "AutoColorization", 0);
        WriteAccent(color);
        BroadcastColorChange();
        RefreshAppAccent(color);
    }

    private static void WriteAccent(Color color)
    {
        var abgr = ToAbgr(color);
        WriteInt(DwmKey, "AccentColor", abgr);
        WriteInt(DwmKey, "ColorizationColor", abgr);
        WriteInt(DwmKey, "ColorizationAfterglow", abgr);
        WriteInt(AccentKey, "AccentColor", abgr);
        WriteInt(AccentKey, "AccentColorMenu", abgr);
        WriteInt(AccentKey, "StartColorMenu", abgr);
        WriteBinary(AccentKey, "AccentPalette", BuildPalette(color));
    }

    private static int ToAbgr(Color color) =>
        unchecked((int)(0xFF000000 | (uint)(color.B << 16) | (uint)(color.G << 8) | color.R));

    private static void SaveAutomatic(bool enabled)
    {
        try
        {
            SalsaNOWConfig.Update(config => config.AccentSourceAutomatic = enabled);
        }
        catch
        {
        }

        try
        {
            ApplicationData.Current.LocalSettings.Values[AccentSourceSetting] = enabled;
        }
        catch
        {
        }
    }

    private static void RefreshAppAccent(Color color)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            return;
        }

        ApplyAccentToDictionary(resources, color);
        foreach (var theme in resources.ThemeDictionaries.Values.OfType<ResourceDictionary>())
        {
            ApplyAccentToDictionary(theme, color);
        }

        ApplyAppTheme();
    }

    private static void ApplyAccentToDictionary(ResourceDictionary resources, Color color)
    {
        resources["SystemAccentColor"] = color;
        resources["SystemAccentColorLight1"] = color;
        resources["SystemAccentColorLight2"] = color;
        resources["SystemAccentColorLight3"] = color;
        resources["SystemAccentColorDark1"] = color;
        resources["SystemAccentColorDark2"] = color;
        resources["SystemAccentColorDark3"] = color;
        resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(color);
        resources["AccentFillColorSecondaryBrush"] = new SolidColorBrush(color);
        resources["AccentFillColorTertiaryBrush"] = new SolidColorBrush(color);
        resources["ToggleSwitchFillOn"] = new SolidColorBrush(color);
        resources["ToggleSwitchFillOnPointerOver"] = new SolidColorBrush(color);
        resources["ToggleSwitchFillOnPressed"] = new SolidColorBrush(color);
        resources["ToggleSwitchKnobFillOn"] = new SolidColorBrush(Microsoft.UI.Colors.White);
    }

    public static IReadOnlyList<Color> GetRecentColors()
    {
        try
        {
            SalsaNOWConfig.EnsureExists();
            var fromConfig = SalsaNOWConfig.Current.RecentAccentColors
                .Select(TryParseHex)
                .OfType<Color>()
                .ToList();
            if (fromConfig.Count > 0)
            {
                return fromConfig;
            }
        }
        catch
        {
        }

        try
        {
            if (ApplicationData.Current.LocalSettings.Values[RecentSetting] is string stored &&
                !string.IsNullOrWhiteSpace(stored))
            {
                var parsed = stored
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(TryParseHex)
                    .OfType<Color>()
                    .ToList();
                if (parsed.Count > 0)
                {
                    return parsed;
                }
            }
        }
        catch
        {
        }

        return [GetAccent()];
    }

    public static void ApplyAppTheme()
    {
        var light = GetAppsUseLightTheme();
        if (App.MainWindow?.Content is FrameworkElement root)
        {
            root.RequestedTheme = light ? ElementTheme.Light : ElementTheme.Dark;
        }

        ApplyCaptionButtons(light);
    }

    private static void ApplyCaptionButtons(bool light)
    {
        try
        {
            var titleBar = App.MainWindow?.AppWindow.TitleBar;
            if (titleBar is null)
            {
                return;
            }

            var foreground = light ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.White;
            var dim = light
                ? Color.FromArgb(153, 0, 0, 0)
                : Color.FromArgb(153, 255, 255, 255);
            var hover = light
                ? Color.FromArgb(18, 0, 0, 0)
                : Color.FromArgb(24, 255, 255, 255);
            var pressed = light
                ? Color.FromArgb(32, 0, 0, 0)
                : Color.FromArgb(40, 255, 255, 255);

            titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonInactiveForegroundColor = dim;
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedForegroundColor = foreground;
            titleBar.ButtonHoverBackgroundColor = hover;
            titleBar.ButtonPressedBackgroundColor = pressed;
        }
        catch
        {
        }
    }

    public static bool ColorsEqual(Color left, Color right) =>
        left.R == right.R && left.G == right.G && left.B == right.B;

    private static void RememberRecent(Color color)
    {
        var colors = GetRecentColors()
            .Where(existing => !ColorsEqual(existing, color))
            .Prepend(color)
            .Take(5)
            .ToList();

        var hex = colors.Select(ToHex).ToList();
        try
        {
            SalsaNOWConfig.Update(config =>
            {
                config.AccentColor = ToHex(color);
                config.RecentAccentColors = hex;
            });
        }
        catch
        {
        }

        try
        {
            ApplicationData.Current.LocalSettings.Values[RecentSetting] = string.Join(',', hex);
        }
        catch
        {
        }
    }

    private static void PersistColors(ColorMode? mode = null)
    {
        try
        {
            var accent = GetAccent();
            var resolved = mode ?? GetMode();
            SalsaNOWConfig.Update(config =>
            {
                config.ColorMode = resolved switch
                {
                    ColorMode.Light => "light",
                    ColorMode.Custom => "custom",
                    _ => "dark"
                };
                config.WindowsMode = GetSystemUsesLightTheme() ? "light" : "dark";
                config.AppsMode = GetAppsUseLightTheme() ? "light" : "dark";
                config.Transparency = GetTransparency();
                config.AccentColor = ToHex(accent);
            });
        }
        catch
        {
        }
    }

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static Color? TryParseHex(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.TrimStart('#');
        if (value.Length != 6 ||
            !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return null;
        }

        return Color.FromArgb(255, (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
    }

    private static byte[] BuildPalette(Color color)
    {
        Color[] ramp =
        [
            Shade(color, 0.55),
            Shade(color, 0.7),
            Shade(color, 0.85),
            color,
            color,
            color,
            color,
            color,
        ];

        var bytes = new byte[32];
        for (var i = 0; i < ramp.Length; i++)
        {
            bytes[i * 4] = ramp[i].R;
            bytes[i * 4 + 1] = ramp[i].G;
            bytes[i * 4 + 2] = ramp[i].B;
            bytes[i * 4 + 3] = 0xFF;
        }

        return bytes;
    }

    private static Color Shade(Color color, double amount) =>
        Color.FromArgb(255, (byte)(color.R * amount), (byte)(color.G * amount), (byte)(color.B * amount));

    private static Color Tint(Color color, double amount) =>
        Color.FromArgb(
            255,
            (byte)(color.R + ((255 - color.R) * amount)),
            (byte)(color.G + ((255 - color.G) * amount)),
            (byte)(color.B + ((255 - color.B) * amount)));

    private static int ReadInt(string keyPath, string name, int fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(name) switch
            {
                int number => number,
                string text when int.TryParse(text, out var parsed) => parsed,
                _ => fallback
            };
        }
        catch
        {
            return fallback;
        }
    }

    private static void WriteInt(string keyPath, string name, int value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath);
            key?.SetValue(name, value, RegistryValueKind.DWord);
        }
        catch
        {
        }
    }

    private static void WriteBinary(string keyPath, string name, byte[] value)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(keyPath);
            key?.SetValue(name, value, RegistryValueKind.Binary);
        }
        catch
        {
        }
    }

    private static void BroadcastColorChange()
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            SendNotifyMessage((nint)0xFFFF, 0x001A, nint.Zero, "ImmersiveColorSet");
            SendNotifyMessage((nint)0xFFFF, 0x0320, nint.Zero, null!);
        });
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SendNotifyMessage(nint hWnd, uint msg, nint wParam, string lParam);
}
