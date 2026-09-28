using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SalsaNOWSettings.Helpers;

public static class WindowsWallpaperCatalog
{
    private static readonly string[] PreferredFiles =
    [
        @"Web\Wallpaper\Windows\img0.jpg",
        @"Web\Wallpaper\Windows\img19.jpg",
        @"Web\Wallpaper\ThemeA\img20.jpg",
        @"Web\Wallpaper\ThemeB\img24.jpg",
        @"Web\Wallpaper\ThemeC\img28.jpg",
        @"Web\Wallpaper\ThemeD\img32.jpg",
    ];

    public static IReadOnlyList<string> GetThemePreviews(int count = 6)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var picks = new List<string>();

        foreach (var relative in PreferredFiles)
        {
            var path = Path.Combine(windows, relative);
            if (File.Exists(path))
            {
                picks.Add(path);
            }
        }

        if (picks.Count >= count)
        {
            return picks.Take(count).ToList();
        }

        var wallpaperRoot = Path.Combine(windows, @"Web\Wallpaper");
        if (!Directory.Exists(wallpaperRoot))
        {
            return picks;
        }

        foreach (var path in Directory.EnumerateFiles(wallpaperRoot, "*.jpg", SearchOption.AllDirectories))
        {
            if (picks.Exists(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            picks.Add(path);
            if (picks.Count >= count)
            {
                break;
            }
        }

        return picks;
    }

    public static IReadOnlyList<string> GetAllWallpapers()
    {
        var system = new List<string>();
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Web\Wallpaper");
        if (Directory.Exists(root))
        {
            system.AddRange(Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(IsImage)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        }

        var custom = GetCustomWallpaper();
        if (custom is not null &&
            !system.Exists(path => DesktopWallpaper.PathsEqual(path, custom)))
        {
            system.Insert(0, custom);
        }

        return system;
    }

    private static string? GetCustomWallpaper()
    {
        try
        {
            SalsaNOWConfig.EnsureExists();
            var wallpaper = SalsaNOWConfig.Current.Wallpaper;
            if (string.IsNullOrWhiteSpace(wallpaper) || !File.Exists(wallpaper) || !IsImage(wallpaper))
            {
                return null;
            }

            var windows = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Web\Wallpaper");
            if (wallpaper.StartsWith(windows, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return Path.GetFullPath(wallpaper);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsImage(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }
}
