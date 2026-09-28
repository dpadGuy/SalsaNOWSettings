using System;
using System.IO;
using System.Text;

namespace SalsaNOWSettings.Helpers;

public static class StartupBatchSettings
{
    public const string FilePath = @"I:\Apps\SalsaNOW\StartupBatch.bat";
    public const string DisabledPath = @"I:\Apps\SalsaNOW\StartupBatch.disabled.bat";

    public static readonly string DefaultScript = string.Join(
        "\r\n",
        "@echo off",
        "setlocal",
        "title SalsaNOW startup",
        "rem SalsaNOW runs this file when a GeForce NOW session starts.",
        "rem Add anything you want chained here.",
        "",
        "exit /b 0",
        "");

    private static readonly Encoding BatchEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static bool IsEnabled() => File.Exists(FilePath);

    public static string Read()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return NormalizeBatch(File.ReadAllText(FilePath));
            }

            if (File.Exists(DisabledPath))
            {
                return NormalizeBatch(File.ReadAllText(DisabledPath));
            }
        }
        catch
        {
        }

        return DefaultScript;
    }

    public static bool TryEnable(out string error)
    {
        try
        {
            WriteBatch(FilePath, Read());
            DeleteIfExists(DisabledPath);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryDisable(string script, out string error)
    {
        try
        {
            WriteBatch(DisabledPath, script);
            DeleteIfExists(FilePath);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TrySave(string script, out string error)
    {
        try
        {
            WriteBatch(IsEnabled() ? FilePath : DisabledPath, script);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void WriteBatch(string path, string? script)
    {
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(path, NormalizeBatch(script), BatchEncoding);
    }

    private static string NormalizeBatch(string? script)
    {
        var text = (script ?? string.Empty).TrimStart('\uFEFF')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\r\n", StringComparison.Ordinal);
        return text.EndsWith("\r\n", StringComparison.Ordinal) || text.Length == 0
            ? text
            : text + "\r\n";
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
