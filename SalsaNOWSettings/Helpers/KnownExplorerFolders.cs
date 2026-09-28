using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SalsaNOWSettings.Helpers;

internal static class KnownExplorerFolders
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    public static string Profile =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string Desktop =>
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string Documents =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public static string Pictures =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    public static string Music =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

    public static string Videos =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

    public static string Downloads
    {
        get
        {
            try
            {
                var id = DownloadsId;
                var hr = SHGetKnownFolderPath(ref id, 0, nint.Zero, out var pointer);
                if (hr == 0 && pointer != nint.Zero)
                {
                    var path = Marshal.PtrToStringUni(pointer) ?? string.Empty;
                    Marshal.FreeCoTaskMem(pointer);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return path;
                    }
                }
            }
            catch
            {
            }

            return Path.Combine(Profile, "Downloads");
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(
        ref Guid rfid,
        uint dwFlags,
        nint hToken,
        out nint ppszPath);
}
