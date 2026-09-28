using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace SalsaNOWSettings.Helpers;

public sealed record AssociationInfo(string Type, bool IsProtocol, string CurrentApp);

public sealed record DefaultAppMapping(string Type, string ProgId, string Application);

public static class WindowsDefaultApps
{
    public const string FolderPath = SalsaNOWConfig.FolderPath;
    public const string ConfigPath = SalsaNOWConfig.FilePath;
    public const string SetUserFtaPreferredPath = @"I:\Apps\SalsaNOW\SilentApps\SetUserFTA.exe";
    public const string SetUserFtaFallbackPath = @"I:\Apps\SalsaNOW\SiletnApps\SetUserFTA.exe";

    private static readonly string[] CommonProtocols =
    [
        "http", "https", "mailto", "ftp", "ftps", "steam", "discord",
        "spotify", "tg", "calculator", "ms-settings", "microsoft-edge"
    ];

    public static string SetUserFtaPath =>
        File.Exists(SetUserFtaPreferredPath) ? SetUserFtaPreferredPath
        : File.Exists(SetUserFtaFallbackPath) ? SetUserFtaFallbackPath
        : SetUserFtaPreferredPath;

    public static string PreferredAppFolder =>
        Directory.Exists(@"I:\Apps") ? @"I:\Apps"
        : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

    public static List<AssociationInfo> LoadCatalog()
    {
        var items = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var exts = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts");
            if (exts is not null)
            {
                foreach (var name in exts.GetSubKeyNames())
                {
                    if (IsExtension(name))
                    {
                        items[name] = false;
                    }
                }
            }
        }
        catch
        {
        }

        try
        {
            using var classes = Registry.ClassesRoot;
            foreach (var name in classes.GetSubKeyNames())
            {
                if (IsExtension(name))
                {
                    items.TryAdd(name, false);
                }
            }
        }
        catch
        {
        }

        foreach (var protocol in CommonProtocols)
        {
            items.TryAdd(protocol, true);
        }

        foreach (var mapping in LoadMappings())
        {
            items[mapping.Type] = !mapping.Type.StartsWith('.');
        }

        return items
            .Select(pair => new AssociationInfo(pair.Key, pair.Value, string.Empty))
            .OrderBy(item => item.Type, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<AssociationInfo> Search(IReadOnlyList<AssociationInfo> catalog, string query, int limit = 40)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            return [];
        }

        return catalog
            .Where(item => Matches(item.Type, query))
            .Take(limit)
            .Select(Describe)
            .ToList();
    }

    public static AssociationInfo? Resolve(string query)
    {
        var type = Normalize(query);
        return type is null ? null : Describe(new AssociationInfo(type, !type.StartsWith('.'), string.Empty));
    }

    public static AssociationInfo Describe(AssociationInfo item) =>
        item with { CurrentApp = GetFriendlyApp(item.Type) };

    public static string GetFriendlyApp(string type)
    {
        var windows = QueryAssoc(type, AssocStr.FriendlyAppName)
                      ?? QueryAssoc(type, AssocStr.Executable);
        if (!string.IsNullOrWhiteSpace(windows))
        {
            return File.Exists(windows) ? DisplayName(windows) : windows;
        }

        return TryGetMapping(type, out var mapping)
            ? DisplayName(mapping.Application)
            : string.Empty;
    }

    public static bool TryApply(string type, string application, out string error)
    {
        var normalized = Normalize(type);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "Enter a file type or link type first.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(application) || !File.Exists(application))
        {
            error = "Choose an application (.exe).";
            return false;
        }

        if (!File.Exists(SetUserFtaPath))
        {
            error = "SetUserFTA.exe not found. Skipping file associations.";
            return false;
        }

        try
        {
            var capabilities = FindAppCapabilities(application);
            if (capabilities is null && IsBrowserType(normalized))
            {
                capabilities = RegisterBrowserClient(application);
            }

            foreach (var pair in AssociationsToApply(normalized, application, capabilities))
            {
                if (!ProgIdExists(pair.Value))
                {
                    RegisterHandler(pair.Key, pair.Value, application);
                }

                SetUserFta(SetUserFtaPath, pair.Key, pair.Value);
                SaveMapping(new DefaultAppMapping(pair.Key, pair.Value, application));
            }

            if (capabilities is not null)
            {
                SetStartMenuInternet(capabilities.ClientName);
            }

            NotifyAssociationsChanged();
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static IReadOnlyList<DefaultAppMapping> LoadMappings()
    {
        SalsaNOWConfig.EnsureExists();
        return SalsaNOWConfig.Current.Associations;
    }

    public static string GetDefaultBrowser()
    {
        SalsaNOWConfig.EnsureExists();
        return SalsaNOWConfig.Current.DefaultBrowser ?? string.Empty;
    }

    public static string GetDefaultBrowserName()
    {
        var path = GetDefaultBrowser();
        return string.IsNullOrWhiteSpace(path) ? string.Empty : DisplayName(path);
    }

    public static bool TrySetDefaultBrowser(string application, out string error)
    {
        if (string.IsNullOrWhiteSpace(application) || !File.Exists(application))
        {
            error = "Choose an application (.exe).";
            return false;
        }

        SalsaNOWConfig.Update(config => config.DefaultBrowser = application);
        error = string.Empty;
        return true;
    }

    public static string? Normalize(string? query)
    {
        var value = query?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return null;
        }

        if (value.Contains("://", StringComparison.Ordinal))
        {
            return value.Split(':', 2)[0];
        }

        if (value.StartsWith('.'))
        {
            return value.Length > 1 ? value : null;
        }

        if (value.All(character => char.IsLetterOrDigit(character) || character is '-' or '+'))
        {
            return CommonProtocols.Contains(value, StringComparer.OrdinalIgnoreCase)
                ? value
                : $".{value.TrimStart('.')}";
        }

        return value;
    }

    private static bool TryGetMapping(string type, out DefaultAppMapping mapping)
    {
        mapping = LoadMappings().FirstOrDefault(item =>
            string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase))!;
        return mapping is not null;
    }

    private static void SaveMapping(DefaultAppMapping mapping)
    {
        SalsaNOWConfig.Update(config =>
        {
            config.SetUserFta = SetUserFtaPath;
            config.Associations = config.Associations
                .Where(item => !string.Equals(item.Type, mapping.Type, StringComparison.OrdinalIgnoreCase))
                .Append(mapping)
                .OrderBy(item => item.Type, StringComparer.OrdinalIgnoreCase)
                .ToList();
        });
    }

    private static readonly string[] BrowserTypes =
    [
        "http", "https", "ftp",
        ".htm", ".html", ".shtml", ".xht", ".xhtml"
    ];

    private static bool IsBrowserType(string type) =>
        BrowserTypes.Contains(type, StringComparer.OrdinalIgnoreCase);

    private static string[] RelatedTypes(string type) =>
        IsBrowserType(type) ? BrowserTypes : [type];

    private static Dictionary<string, string> AssociationsToApply(
        string type,
        string application,
        AppCapabilities? capabilities)
    {
        var jobs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (IsBrowserType(type) && capabilities is not null)
        {
            foreach (var pair in capabilities.Associations.Where(item => IsBrowserType(item.Key)))
            {
                jobs[pair.Key] = pair.Value;
            }
        }

        foreach (var target in RelatedTypes(type))
        {
            if (jobs.ContainsKey(target))
            {
                continue;
            }

            jobs[target] = capabilities is not null && capabilities.Associations.TryGetValue(target, out var progId)
                ? progId
                : ProgIdFor(application, target);
        }

        return jobs;
    }

    private static string ProgIdFor(string application, string? type = null)
    {
        var existing = LoadMappings().FirstOrDefault(item =>
            string.Equals(item.Application, application, StringComparison.OrdinalIgnoreCase) &&
            (type is null || string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase)));
        if (existing is not null && !string.IsNullOrWhiteSpace(existing.ProgId))
        {
            return existing.ProgId;
        }

        var name = Path.GetFileNameWithoutExtension(application) ?? "App";
        var safe = new string(name.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrEmpty(safe))
        {
            safe = "App";
        }

        return type is not null && !type.StartsWith('.')
            ? safe + "URL.Custom"
            : safe + "HTML.Custom";
    }

    private sealed class AppCapabilities
    {
        public string ClientName { get; init; } = string.Empty;
        public Dictionary<string, string> Associations { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static AppCapabilities? FindAppCapabilities(string application)
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var registered = hive.OpenSubKey(@"Software\RegisteredApplications");
            if (registered is not null)
            {
                foreach (var name in registered.GetValueNames())
                {
                    if (registered.GetValue(name) is not string capPath ||
                        !CapabilitiesMatchExe(hive, capPath, application))
                    {
                        continue;
                    }

                    var found = ReadCapabilities(hive, capPath, name);
                    if (found.Associations.Count > 0)
                    {
                        return found;
                    }
                }
            }

            foreach (var path in new[]
            {
                @"SOFTWARE\Clients\StartMenuInternet",
                @"SOFTWARE\WOW6432Node\Clients\StartMenuInternet"
            })
            {
                using var root = hive.OpenSubKey(path);
                if (root is null)
                {
                    continue;
                }

                foreach (var name in root.GetSubKeyNames())
                {
                    if (!CapabilitiesMatchExe(hive, path + @"\" + name + @"\Capabilities", application))
                    {
                        continue;
                    }

                    var found = ReadCapabilities(hive, path + @"\" + name + @"\Capabilities", name);
                    if (found.Associations.Count > 0)
                    {
                        return found;
                    }
                }
            }
        }

        return null;
    }

    private static bool CapabilitiesMatchExe(RegistryKey hive, string capPath, string exe)
    {
        using var caps = hive.OpenSubKey(capPath);
        var icon = caps?.GetValue("ApplicationIcon")?.ToString();
        var comma = icon?.LastIndexOf(',');
        var iconPath = (comma > 0 ? icon![..comma.Value] : icon)?.Trim().Trim('"');
        if (PathsEqual(iconPath, exe) || SameDirectory(iconPath, exe))
        {
            return true;
        }

        var clientPath = capPath.EndsWith(@"\Capabilities", StringComparison.OrdinalIgnoreCase)
            ? capPath[..^@"\Capabilities".Length]
            : capPath;
        using var client = hive.OpenSubKey(clientPath);
        var command = ReadCommand(client, @"shell\open\command");
        return CommandUsesExe(command, exe) || SameDirectory(ExtractCommandPath(command), exe);
    }

    private static AppCapabilities ReadCapabilities(RegistryKey hive, string capPath, string clientName)
    {
        var associations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var caps = hive.OpenSubKey(capPath);
        AddAssociations(caps?.OpenSubKey("URLAssociations"), associations);
        AddAssociations(caps?.OpenSubKey("FileAssociations"), associations);
        return new AppCapabilities
        {
            ClientName = clientName,
            Associations = associations
        };
    }

    private static void AddAssociations(RegistryKey? key, Dictionary<string, string> associations)
    {
        if (key is null)
        {
            return;
        }

        foreach (var name in key.GetValueNames())
        {
            if (key.GetValue(name) is string progId && !string.IsNullOrWhiteSpace(progId))
            {
                associations[name] = progId;
            }
        }
    }

    private static AppCapabilities RegisterBrowserClient(string exePath)
    {
        var name = Path.GetFileNameWithoutExtension(exePath) ?? "Browser";
        var client = new string(name.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrEmpty(client))
        {
            client = "Browser";
        }

        client += ".Custom";
        var htmlId = client.Replace(".Custom", "HTML.Custom", StringComparison.Ordinal);
        var urlId = client.Replace(".Custom", "URL.Custom", StringComparison.Ordinal);
        RegisterHandler(".html", htmlId, exePath);
        RegisterHandler("https", urlId, exePath);

        var clientPath = @"Software\Clients\StartMenuInternet\" + client;
        using (var key = Registry.CurrentUser.CreateSubKey(clientPath))
        {
            key?.SetValue("", name);
            key?.SetValue("LocalizedString", name);
        }

        using (var command = Registry.CurrentUser.CreateSubKey(clientPath + @"\shell\open\command"))
        {
            command?.SetValue("", "\"" + exePath + "\"", RegistryValueKind.String);
        }

        using (var icon = Registry.CurrentUser.CreateSubKey(clientPath + @"\DefaultIcon"))
        {
            icon?.SetValue("", exePath + ",0", RegistryValueKind.String);
        }

        var capPath = clientPath + @"\Capabilities";
        using (var caps = Registry.CurrentUser.CreateSubKey(capPath))
        {
            caps?.SetValue("ApplicationName", name);
            caps?.SetValue("ApplicationIcon", exePath + ",0");
            caps?.SetValue("ApplicationDescription", name);
        }

        using (var urls = Registry.CurrentUser.CreateSubKey(capPath + @"\URLAssociations"))
        {
            urls?.SetValue("http", urlId);
            urls?.SetValue("https", urlId);
        }

        using (var files = Registry.CurrentUser.CreateSubKey(capPath + @"\FileAssociations"))
        {
            files?.SetValue(".htm", htmlId);
            files?.SetValue(".html", htmlId);
        }

        using (var registered = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
        {
            registered?.SetValue(client, capPath);
        }

        return new AppCapabilities
        {
            ClientName = client,
            Associations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["http"] = urlId,
                ["https"] = urlId,
                [".htm"] = htmlId,
                [".html"] = htmlId
            }
        };
    }

    private static void SetStartMenuInternet(string clientName)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Clients\StartMenuInternet");
        key?.SetValue("", clientName);
    }

    private static bool ProgIdExists(string progId) =>
        !string.IsNullOrWhiteSpace(ReadProgIdCommand(progId));

    private static void RegisterHandler(string type, string progId, string exePath)
    {
        using var prog = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + progId)
            ?? throw new InvalidOperationException("Could not create " + progId);
        if (!type.StartsWith('.'))
        {
            prog.SetValue("URL Protocol", string.Empty, RegistryValueKind.String);
        }

        using var command = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + progId + @"\shell\open\command")
            ?? throw new InvalidOperationException("Could not create open command.");
        command.SetValue("", OpenCommand(exePath, !type.StartsWith('.')), RegistryValueKind.String);
    }

    private static void RegisterOpenCommand(string keyPath, string exePath)
    {
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException("Skipping file association command, missing: " + exePath, exePath);
        }

        using var key = Registry.CurrentUser.CreateSubKey(keyPath)
            ?? throw new InvalidOperationException("Could not create " + keyPath);
        key.SetValue("", "\"" + exePath + "\" \"%1\"", RegistryValueKind.String);
    }

    private static string OpenCommand(string exePath, bool isProtocol)
    {
        var name = Path.GetFileNameWithoutExtension(exePath) ?? string.Empty;
        if (isProtocol && ContainsAny(name, "firefox", "waterfox", "librewolf", "zen", "floorp"))
        {
            return "\"" + exePath + "\" -osint -url \"%1\"";
        }

        return "\"" + exePath + "\" \"%1\"";
    }

    private static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static void NotifyAssociationsChanged()
    {
        SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }

    private static void SetUserFta(string setUserFta, string extension, string progId)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = setUserFta,
            Arguments = extension + " " + progId,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
        process?.WaitForExit();
    }

    private static string? ReadCommand(RegistryKey? key, string path) =>
        key?.OpenSubKey(path)?.GetValue(null)?.ToString();

    private static string? ReadProgIdCommand(string progId) =>
        ReadCommand(Registry.CurrentUser, @"Software\Classes\" + progId + @"\shell\open\command")
        ?? ReadCommand(Registry.ClassesRoot, progId + @"\shell\open\command");

    private static bool CommandUsesExe(string? command, string exe)
    {
        var path = ExtractCommandPath(command);
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch
        {
        }

        return string.Equals(Path.GetFileName(path), Path.GetFileName(exe), StringComparison.OrdinalIgnoreCase)
               || SameDirectory(path, exe);
    }

    private static bool PathsEqual(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
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

    private static bool SameDirectory(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return false;
        }

        try
        {
            var leftDir = Path.GetDirectoryName(Path.GetFullPath(left));
            var rightDir = Path.GetDirectoryName(Path.GetFullPath(right));
            return !string.IsNullOrEmpty(leftDir) &&
                   string.Equals(leftDir, rightDir, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? ExtractCommandPath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var value = command.Trim();
        if (value.StartsWith('"'))
        {
            var end = value.IndexOf('"', 1);
            return end > 1 ? value[1..end] : value.Trim('"');
        }

        var space = value.IndexOf(' ');
        return space < 0 ? value : value[..space];
    }

    private static string DisplayName(string application)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(application);
            if (!string.IsNullOrWhiteSpace(info.FileDescription))
            {
                return info.FileDescription;
            }
        }
        catch
        {
        }

        return Path.GetFileNameWithoutExtension(application);
    }

    private static bool Matches(string type, string query)
    {
        return type.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               type.TrimStart('.').StartsWith(query.TrimStart('.'), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExtension(string name) =>
        name.StartsWith('.') && name.Length is > 1 and < 32 && !name.Contains(' ');

    private static string? QueryAssoc(string assoc, AssocStr kind)
    {
        try
        {
            uint length = 0;
            AssocQueryString(AssocF.None, kind, assoc, null, null, ref length);
            if (length == 0)
            {
                return null;
            }

            var buffer = new StringBuilder((int)length);
            return AssocQueryString(AssocF.None, kind, assoc, null, buffer, ref length) == 0
                ? buffer.ToString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private enum AssocF : uint
    {
        None = 0
    }

    private enum AssocStr
    {
        Executable = 2,
        FriendlyAppName = 4
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(
        AssocF flags,
        AssocStr str,
        string assoc,
        string? extra,
        StringBuilder? buffer,
        ref uint length);
}
