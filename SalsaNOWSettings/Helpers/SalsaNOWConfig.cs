using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SalsaNOWSettings.Helpers;

public sealed class SalsaNOWConfigData
{
    public string SetUserFta { get; set; } = WindowsDefaultApps.SetUserFtaPreferredPath;
    public string DefaultBrowser { get; set; } = string.Empty;
    public List<DefaultAppMapping> Associations { get; set; } = [];
    public string? ColorMode { get; set; }
    public string? WindowsMode { get; set; }
    public string? AppsMode { get; set; }
    public bool? Transparency { get; set; }
    public bool AccentSourceAutomatic { get; set; }
    public string? AccentColor { get; set; }
    public List<string> RecentAccentColors { get; set; } = [];
    public string? TaskbarAlignment { get; set; }
    public bool? TaskbarAutoHide { get; set; }
    public bool? TaskbarBadges { get; set; }
    public bool? TaskbarFlashing { get; set; }
    public bool? TaskbarShareWindows { get; set; }
    public bool? TaskbarShowDesktopCorner { get; set; }
    public string? TaskbarCombine { get; set; }
    public string? TaskbarSmallerButtons { get; set; }
    public bool? TaskbarFullTransparency { get; set; }
    public string? Wallpaper { get; set; }
    public bool? SteamSilentLaunch { get; set; }
    public bool? BingWallpaper { get; set; }
}

public static class SalsaNOWConfig
{
    public const string FolderPath = @"I:\Apps\SalsaNOW";
    public const string FilePath = @"I:\Apps\SalsaNOW\SalsaNOWSettings.json";

    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static SalsaNOWConfigData? _current;

    public static SalsaNOWConfigData Current
    {
        get
        {
            lock (Gate)
            {
                return _current ??= LoadOrCreate();
            }
        }
    }

    public static void EnsureExists()
    {
        lock (Gate)
        {
            _current ??= LoadOrCreate();
        }
    }

    public static void Update(Action<SalsaNOWConfigData> change)
    {
        lock (Gate)
        {
            _current ??= LoadOrCreate();
            change(_current);
            Save(_current);
        }
    }

    private static SalsaNOWConfigData LoadOrCreate()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<SalsaNOWConfigData>(File.ReadAllText(FilePath), JsonOptions);
                if (loaded is not null)
                {
                    loaded.Associations ??= [];
                    loaded.RecentAccentColors ??= [];
                    loaded.DefaultBrowser ??= string.Empty;
                    if (string.IsNullOrWhiteSpace(loaded.SetUserFta))
                    {
                        loaded.SetUserFta = WindowsDefaultApps.SetUserFtaPath;
                    }

                    Hydrate(loaded);
                    Save(loaded);
                    return loaded;
                }
            }
        }
        catch
        {
        }

        var created = new SalsaNOWConfigData
        {
            SetUserFta = WindowsDefaultApps.SetUserFtaPath
        };
        Hydrate(created);
        Save(created);
        return created;
    }

    private static void Hydrate(SalsaNOWConfigData data)
    {
        data.ColorMode = Or(data.ColorMode, WindowsColorSettings.GetMode() switch
        {
            ColorMode.Light => "light",
            ColorMode.Custom => "custom",
            _ => "dark"
        });
        data.WindowsMode = Or(data.WindowsMode, WindowsColorSettings.GetSystemUsesLightTheme() ? "light" : "dark");
        data.AppsMode = Or(data.AppsMode, WindowsColorSettings.GetAppsUseLightTheme() ? "light" : "dark");
        data.Transparency ??= WindowsColorSettings.GetTransparency();
        if (string.IsNullOrWhiteSpace(data.AccentColor))
        {
            var accent = WindowsColorSettings.GetAccent();
            data.AccentColor = $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}";
        }

        data.TaskbarAlignment = Or(data.TaskbarAlignment, WindowsTaskbarSettings.GetAlignment() == 0 ? "left" : "center");
        data.TaskbarAutoHide ??= WindowsTaskbarSettings.GetAutoHide();
        data.TaskbarBadges ??= WindowsTaskbarSettings.GetBadges();
        data.TaskbarFlashing ??= WindowsTaskbarSettings.GetFlashing();
        data.TaskbarShareWindows ??= WindowsTaskbarSettings.GetShareWindows();
        data.TaskbarShowDesktopCorner ??= WindowsTaskbarSettings.GetShowDesktopCorner();
        data.TaskbarCombine = Or(data.TaskbarCombine, CombineName(WindowsTaskbarSettings.GetCombine()));
        data.TaskbarSmallerButtons = Or(data.TaskbarSmallerButtons, SmallerName(WindowsTaskbarSettings.GetSmallerButtons()));
        data.TaskbarFullTransparency ??= TranslucentTb.IsRunning();
    }

    public static string CombineName(int value) => value switch
    {
        1 => "whenFull",
        2 => "never",
        _ => "always"
    };

    public static int CombineValue(string? value) => value switch
    {
        "whenFull" => 1,
        "never" => 2,
        _ => 0
    };

    public static string SmallerName(int value) => value switch
    {
        0 => "always",
        2 => "whenFull",
        _ => "never"
    };

    public static int SmallerValue(string? value) => value switch
    {
        "always" => 0,
        "whenFull" => 2,
        _ => 1
    };

    private static string Or(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static void Save(SalsaNOWConfigData data)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonOptions) + "\r\n", Utf8);
        }
        catch
        {
        }
    }
}
