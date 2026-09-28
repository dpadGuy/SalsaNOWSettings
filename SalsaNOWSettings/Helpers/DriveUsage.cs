using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SalsaNOWSettings.Helpers;

public sealed record DriveSummary(bool Ready, long Total, long Used, long Free, bool Persistent)
{
    public static DriveSummary Missing { get; } = new(false, 0, 0, 0, false);

    public double UsedPercent => Total <= 0 ? 0 : Used * 100.0 / Total;
}

public sealed record StorageEntry(string Name, string Path, bool IsDirectory, long Size);

public static class DriveUsage
{
    public const string DriveLetter = "I";
    public const string RootPath = @"I:\";
    private const long EphemeralLimit = 110L * 1024 * 1024 * 1024;

    public static DriveSummary ReadDrive()
    {
        try
        {
            var drive = new DriveInfo(DriveLetter);
            if (!drive.IsReady)
            {
                return DriveSummary.Missing;
            }

            var total = drive.TotalSize;
            var free = drive.TotalFreeSpace;
            var used = total > free ? total - free : 0;
            return new DriveSummary(true, total, used, free, total > EphemeralLimit);
        }
        catch
        {
            return DriveSummary.Missing;
        }
    }

    public static IReadOnlyList<StorageEntry> ListLargest(string path, int limit = 150)
    {
        DirectoryInfo folder;
        try
        {
            folder = new DirectoryInfo(path);
            if (!folder.Exists)
            {
                return [];
            }
        }
        catch
        {
            return [];
        }

        FileSystemInfo[] children;
        try
        {
            children = folder.GetFileSystemInfos();
        }
        catch
        {
            return [];
        }

        var found = new ConcurrentBag<StorageEntry>();
        Parallel.ForEach(children, child =>
        {
            try
            {
                if (child.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return;
                }

                var size = child is FileInfo file ? file.Length : FolderSize(child.FullName);
                found.Add(new StorageEntry(child.Name, child.FullName, child is DirectoryInfo, size));
            }
            catch
            {
            }
        });

        return found
            .OrderByDescending(item => item.Size)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    public static bool TryResolveFolder(string path, out string full)
    {
        full = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            full = Path.GetFullPath(path.Trim().Trim('"'));
            if (!Directory.Exists(full))
            {
                return false;
            }

            var root = Path.GetFullPath(RootPath).TrimEnd('\\');
            var trimmed = full.TrimEnd('\\');
            if (!string.Equals(trimmed, root, StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string? ParentPath(string path)
    {
        try
        {
            var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetFullPath(RootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return Path.GetDirectoryName(full);
        }
        catch
        {
            return null;
        }
    }

    public static bool TryDelete(string path, out string error)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetFullPath(RootPath).TrimEnd('\\');
            if (string.Equals(full.TrimEnd('\\'), root, StringComparison.OrdinalIgnoreCase))
            {
                error = "The I: drive itself cannot be deleted.";
                return false;
            }

            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                error = "Only items on I: can be deleted here.";
                return false;
            }

            if (Directory.Exists(full))
            {
                Directory.Delete(full, true);
            }
            else if (File.Exists(full))
            {
                File.SetAttributes(full, FileAttributes.Normal);
                File.Delete(full);
            }
            else
            {
                error = "That item is already gone.";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool IsSalsaNowFolder(string path)
    {
        try
        {
            var full = Path.GetFullPath(path).TrimEnd('\\');
            var salsa = Path.GetFullPath(@"I:\Apps\SalsaNOW").TrimEnd('\\');
            return string.Equals(full, salsa, StringComparison.OrdinalIgnoreCase) ||
                   full.StartsWith(salsa + "\\", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
        {
            bytes = 0;
        }

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

        if (bytes >= mb)
        {
            return $"{bytes / mb:0.0} MB";
        }

        if (bytes >= kb)
        {
            return $"{bytes / kb:0} KB";
        }

        return $"{bytes} B";
    }

    private static long FolderSize(string path)
    {
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return total;
    }
}
