using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace SalsaNOWSettings.Helpers;

public static class DesktopWallpaper
{
    private const int SpiSetDeskWallpaper = 20;
    private const int SpiGetDeskWallpaper = 0x0073;
    private const int SpifUpdateIniFile = 0x01;
    private const int SpifSendWinIniChange = 0x02;

    public static string StagingFolder => Path.Combine(SalsaNOWConfig.FolderPath, "DesktopWallpaper");

    public static bool Apply(string path, bool bingWallpaper = false)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        var source = Path.GetFullPath(path);
        var staged = StageCopy(source);
        Remember(source, staged, bingWallpaper);
        return SetDesktop(staged ?? source);
    }

    public static string? GetCurrentPath()
    {
        var buffer = new StringBuilder(2048);
        if (SystemParametersInfo(SpiGetDeskWallpaper, buffer.Capacity, buffer, 0))
        {
            var path = ExpandExisting(buffer.ToString());
            if (path is not null)
            {
                return path;
            }
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var path = ExpandExisting(key?.GetValue("Wallpaper") as string);
            if (path is not null)
            {
                return path;
            }
        }
        catch
        {
        }

        var transcoded = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Themes\TranscodedWallpaper");
        return File.Exists(transcoded) ? transcoded : null;
    }

    public static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static readonly string[] StagingExtensions =
    [
        ".bmp",
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".tif",
        ".tiff",
        ".webp",
        ".jxr"
    ];

    public static string? GetSourcePath()
    {
        try
        {
            SalsaNOWConfig.EnsureExists();
            var wallpaper = SalsaNOWConfig.Current.Wallpaper;
            return File.Exists(wallpaper) ? wallpaper : GetCurrentPath();
        }
        catch
        {
            return GetCurrentPath();
        }
    }

    public static bool Matches(string? candidate, string? current)
    {
        if (PathsEqual(candidate, current))
        {
            return true;
        }

        try
        {
            SalsaNOWConfig.EnsureExists();
            return PathsEqual(candidate, SalsaNOWConfig.Current.Wallpaper);
        }
        catch
        {
            return false;
        }
    }

    private static bool SetDesktop(string path)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true);
            key?.SetValue("WallpaperStyle", "10");
            key?.SetValue("TileWallpaper", "0");
            key?.SetValue("Wallpaper", path);
        }
        catch
        {
        }

        var applied = SystemParametersInfo(SpiSetDeskWallpaper, 0, path, SpifUpdateIniFile | SpifSendWinIniChange);

        try
        {
            var wallpaper = (IDesktopWallpaper)new DesktopWallpaperCoclass();
            wallpaper.SetWallpaper(nint.Zero, path);
            applied = true;
        }
        catch
        {
        }

        return applied;
    }

    private static void Remember(string source, string? staged, bool bingWallpaper)
    {
        try
        {
            SalsaNOWConfig.Update(config =>
            {
                config.Wallpaper = staged ?? source;
                config.BingWallpaper = bingWallpaper ? true : null;
            });
        }
        catch
        {
        }
    }

    private static string? StageCopy(string path)
    {
        try
        {
            Directory.CreateDirectory(StagingFolder);

            var extension = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(extension) ||
                !StagingExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                extension = ".jpg";
            }

            var destination = Path.Combine(StagingFolder, "wallpaper" + extension.ToLowerInvariant());
            if (PathsEqual(path, destination))
            {
                return destination;
            }

            foreach (var existingExtension in StagingExtensions)
            {
                var existing = Path.Combine(StagingFolder, "wallpaper" + existingExtension);
                if (File.Exists(existing) && !PathsEqual(existing, destination))
                {
                    TryReplace(existing);
                }
            }

            CopyOverwrite(path, destination);
            return File.Exists(destination) ? destination : null;
        }
        catch
        {
            return null;
        }
    }

    private static void CopyOverwrite(string source, string destination)
    {
        var temp = destination + ".tmp";
        TryReplace(temp);

        using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            input.CopyTo(output);
        }

        File.SetAttributes(temp, FileAttributes.Normal);
        TryReplace(destination);
        File.Move(temp, destination, overwrite: true);
        File.SetAttributes(destination, FileAttributes.Normal);
    }

    private static void TryReplace(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
        catch
        {
        }
    }

    private static string? ExpandExisting(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        path = Environment.ExpandEnvironmentVariables(path.Trim());
        return File.Exists(path) ? path : null;
    }

    public static void SetFit(string fit)
    {
        var style = fit switch
        {
            "Fit" => "6",
            "Stretch" => "2",
            "Tile" => "0",
            "Center" => "0",
            "Span" => "22",
            _ => "10"
        };

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true);
            key?.SetValue("WallpaperStyle", style);
            key?.SetValue("TileWallpaper", fit == "Tile" ? "1" : "0");
        }
        catch
        {
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: false);
            var current = key?.GetValue("Wallpaper") as string;
            if (!string.IsNullOrWhiteSpace(current))
            {
                Apply(current);
            }
        }
        catch
        {
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, string pvParam, int fWinIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, StringBuilder pvParam, int fWinIni);

    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F933")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper(nint monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
    }

    [ComImport]
    [Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    private class DesktopWallpaperCoclass
    {
    }
}
