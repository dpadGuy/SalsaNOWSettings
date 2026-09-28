using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace SalsaNOWSettings.Helpers;

public sealed class WindowsAccount
{
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Subtitle { get; init; } = "Local account";
    public string? PicturePath { get; init; }

    public static WindowsAccount Current { get; } = Load();

    private static WindowsAccount Load()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var userName = SamName(identity.Name);
        if (string.IsNullOrWhiteSpace(userName))
        {
            userName = Environment.UserName;
        }

        var display = FirstNonEmpty(
            GetUserNameEx(3),
            GetLocalFullName(userName),
            userName);

        var principal = GetUserNameEx(8);
        var subtitle = LooksLikeEmail(principal)
            ? principal!
            : "Local account";

        return new WindowsAccount
        {
            UserName = userName,
            DisplayName = display,
            Subtitle = subtitle,
            PicturePath = FindAccountPicture(identity.User?.Value)
        };
    }

    private static string SamName(string? identityName)
    {
        if (string.IsNullOrWhiteSpace(identityName))
        {
            return string.Empty;
        }

        var slash = identityName.LastIndexOf('\\');
        return slash >= 0 ? identityName[(slash + 1)..] : identityName;
    }

    private static string? GetUserNameEx(int format)
    {
        var size = 256u;
        var buffer = new StringBuilder((int)size);
        if (GetUserNameEx(format, buffer, ref size) && buffer.Length > 0)
        {
            return buffer.ToString().Trim();
        }

        return null;
    }

    private static string? GetLocalFullName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        if (NetUserGetInfo(null, userName, 10, out var buffer) != 0 || buffer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var info = Marshal.PtrToStructure<UserInfo10>(buffer);
            return string.IsNullOrWhiteSpace(info.FullName) ? null : info.FullName.Trim();
        }
        finally
        {
            NetApiBufferFree(buffer);
        }
    }

    private static string? FindAccountPicture(string? sid)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            return null;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\{sid}");
            if (key is null)
            {
                return null;
            }

            foreach (var name in new[] { "Image448", "Image1080", "Image240", "Image96", "Image32" })
            {
                if (key.GetValue(name) is string path && File.Exists(path))
                {
                    return path;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private static bool LooksLikeEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains('@') && !value.Contains('\\');

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return Environment.UserName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct UserInfo10
    {
        public string Name;
        public string Comment;
        public string UsrComment;
        public string FullName;
    }

    [DllImport("secur32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetUserNameEx(int nameFormat, StringBuilder userName, ref uint size);

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetUserGetInfo(string? serverName, string userName, int level, out IntPtr buffer);

    [DllImport("netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);
}
