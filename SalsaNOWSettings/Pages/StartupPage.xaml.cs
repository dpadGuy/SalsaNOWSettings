using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SalsaNOWSettings.Helpers;

namespace SalsaNOWSettings.Pages;

public sealed partial class StartupPage : Page
{
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private bool _ready;

    public StartupPage()
    {
        InitializeComponent();
        _saveTimer.Tick += SaveTimer_Tick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ContentColumn.Width = Math.Clamp(e.NewSize.Width - 96, 360, 800);
    }

    private void Apps_Click(object sender, RoutedEventArgs e) =>
        SettingsNavigation.Go("apps");

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SteamSilentSwitch.IsOn = SalsaNOWConfig.Current.SteamSilentLaunch == true;
        SteamInputSwitch.IsOn = SalsaNOWConfig.Current.SteamInput == true;
        var enabled = StartupBatchSettings.IsEnabled();
        EnabledSwitch.IsOn = enabled;
        if (enabled)
        {
            var script = StartupBatchSettings.Read();
            StartupBatchSettings.TrySave(script, out _);
            ShowEditor(script);
        }

        _ready = true;
    }

    private void SteamSilentSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        SalsaNOWConfig.Update(data => data.SteamSilentLaunch = SteamSilentSwitch.IsOn ? true : null);
    }

    private void SteamInputSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        SalsaNOWConfig.Update(data => data.SteamInput = SteamInputSwitch.IsOn ? true : null);
    }

    private void EnabledSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        if (EnabledSwitch.IsOn)
        {
            if (!StartupBatchSettings.TryEnable(out var error))
            {
                _ready = false;
                EnabledSwitch.IsOn = false;
                _ready = true;
                ShowError(error);
                return;
            }

            ClearError();
            ShowEditor(StartupBatchSettings.Read());
            return;
        }

        _saveTimer.Stop();
        if (!StartupBatchSettings.TryDisable(ScriptBox.Text, out var disableError))
        {
            _ready = false;
            EnabledSwitch.IsOn = true;
            _ready = true;
            ShowError(disableError);
            return;
        }

        ClearError();
        EditorHost.Visibility = Visibility.Collapsed;
    }

    private void ScriptBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready || !EnabledSwitch.IsOn)
        {
            return;
        }

        SaveStatus.Text = "Saving…";
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveTimer_Tick(object? sender, object e)
    {
        _saveTimer.Stop();
        SaveNow();
    }

    private void SaveNow()
    {
        if (!EnabledSwitch.IsOn)
        {
            return;
        }

        if (StartupBatchSettings.TrySave(ScriptBox.Text, out var error))
        {
            SaveStatus.Text = "Saved";
            ClearError();
            return;
        }

        SaveStatus.Text = "Couldn't save";
        ShowError(error);
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ClearError()
    {
        ErrorText.Text = string.Empty;
        ErrorText.Visibility = Visibility.Collapsed;
    }

    private void ShowEditor(string script)
    {
        _ready = false;
        ScriptBox.Text = script;
        _ready = true;
        EditorHost.Visibility = Visibility.Visible;
        SaveStatus.Text = "Saved";
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _saveTimer.Stop();
        if (EnabledSwitch.IsOn)
        {
            StartupBatchSettings.TrySave(ScriptBox.Text, out _);
        }
    }
}
