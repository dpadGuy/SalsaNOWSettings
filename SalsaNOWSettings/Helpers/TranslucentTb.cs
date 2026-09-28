using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SalsaNOWSettings.Helpers;

public static class TranslucentTb
{
    public const string FolderPath = @"I:\Apps\SalsaNOW\Zxplorer\TranslucentTB";
    public const string ExePath = @"I:\Apps\SalsaNOW\Zxplorer\TranslucentTB\TranslucentTB.exe";
    public const string SettingsPath = @"I:\Apps\SalsaNOW\Zxplorer\TranslucentTB\settings.json";

    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private const string ClearSettings =
        """
        {
          "$schema": "https://translucenttb.github.io/settings.schema.json",
          "desktop_appearance": {
            "accent": "clear",
            "color": "#00000000",
            "show_line": false
          },
          "visible_window_appearance": {
            "enabled": false
          },
          "maximized_window_appearance": {
            "enabled": false
          },
          "start_opened_appearance": {
            "enabled": false
          },
          "search_opened_appearance": {
            "enabled": false
          },
          "task_view_opened_appearance": {
            "enabled": false
          },
          "battery_saver_appearance": {
            "enabled": false
          },
          "hide_tray": true,
          "disable_saving": true,
          "verbosity": "off",
          "use_xaml_context_menu": false
        }
        """;

    public static bool IsRunning()
    {
        try
        {
            return Process.GetProcessesByName("TranslucentTB").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool GetEnabled()
    {
        try
        {
            SalsaNOWConfig.EnsureExists();
            if (SalsaNOWConfig.Current.TaskbarFullTransparency is bool stored)
            {
                return stored;
            }
        }
        catch
        {
        }

        return IsRunning();
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            SalsaNOWConfig.Update(config => config.TaskbarFullTransparency = enabled);
        }
        catch
        {
        }

        if (enabled)
        {
            WriteClearSettings();
            Restart();
            return;
        }

        Stop();
    }

    public static void ApplyFromConfig()
    {
        if (SalsaNOWConfig.Current.TaskbarFullTransparency is bool enabled)
        {
            SetEnabled(enabled);
        }
    }

    private static void WriteClearSettings()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(SettingsPath, ClearSettings.ReplaceLineEndings("\r\n") + "\r\n", Utf8);
        }
        catch
        {
        }
    }

    private static void Restart()
    {
        Stop();
        Start();
    }

    private static void Start()
    {
        var exe = ResolveExe();
        if (exe is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? FolderPath,
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = Path.GetDirectoryName(exe) ?? FolderPath,
                    UseShellExecute = false
                });
            }
            catch
            {
            }
        }
    }

    private static string? ResolveExe()
    {
        if (File.Exists(ExePath))
        {
            return ExePath;
        }

        try
        {
            if (!Directory.Exists(FolderPath))
            {
                return null;
            }

            return Directory.EnumerateFiles(FolderPath, "TranslucentTB.exe", SearchOption.AllDirectories)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static void Stop()
    {
        try
        {
            foreach (var process in Process.GetProcessesByName("TranslucentTB"))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
        }
    }
}
