using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SalsaNOWSettings.Controls;
using SalsaNOWSettings.Helpers;
using Windows.ApplicationModel.DataTransfer;

namespace SalsaNOWSettings.Pages;

public sealed partial class AboutPage : Page
{
    private DeviceSpecs _specs = new();
    private bool _specsOpen = true;

    public AboutPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ContentColumn.Width = Math.Clamp(e.NewSize.Width - 96, 360, 960);
    }

    private void System_Click(object sender, RoutedEventArgs e) =>
        SettingsNavigation.Go("system");

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _specs = DeviceSpecs.Load();
        if (SettingsShell.Current is { } shell && !string.IsNullOrWhiteSpace(shell.EnvironmentName))
        {
            _specs = new DeviceSpecs
            {
                DeviceName = shell.EnvironmentName,
                Model = _specs.Model,
                Processor = _specs.Processor,
                ProcessorSpeed = _specs.ProcessorSpeed,
                ProcessorCores = _specs.ProcessorCores,
                InstalledRam = _specs.InstalledRam,
                UsableRam = _specs.UsableRam,
                Graphics = _specs.Graphics,
                GraphicsMemory = _specs.GraphicsMemory,
                StorageTotal = _specs.StorageTotal,
                StorageUsed = _specs.StorageUsed,
                HasPersistentStorage = _specs.HasPersistentStorage,
                DeviceId = _specs.DeviceId,
                ProductId = _specs.ProductId,
                SystemType = _specs.SystemType,
                Edition = _specs.Edition,
                OsBuild = _specs.OsBuild,
                PenAndTouch = _specs.PenAndTouch
            };
        }

        StorageValue.Text = _specs.StorageTotal;
        StorageUsed.Text = _specs.StorageUsed;
        StorageWarning.Visibility = _specs.HasPersistentStorage ? Visibility.Collapsed : Visibility.Visible;
        GraphicsValue.Text = _specs.Graphics;
        GraphicsMemory.Text = _specs.GraphicsMemory;
        RamValue.Text = _specs.InstalledRam;
        ProcessorValue.Text = _specs.Processor;
        ProcessorSpeed.Text = _specs.FormatProcessorDetails();
        DeviceNameText.Text = _specs.DeviceName;
        DeviceModelText.Text = _specs.Model;
        SpecDeviceName.Text = _specs.DeviceName;
        SpecProcessor.Text = _specs.FormatProcessorLine();
        SpecGraphics.Text = string.IsNullOrEmpty(_specs.GraphicsMemory)
            ? _specs.Graphics
            : $"{_specs.Graphics}  ({_specs.GraphicsMemory})";
        SpecRam.Text = _specs.FormatRamLine();
        SpecEdition.Text = _specs.Edition;
        SpecOsBuild.Text = _specs.OsBuild;
        SpecDeviceId.Text = _specs.DeviceId;
        SpecProductId.Text = _specs.ProductId;
        SpecSystemType.Text = _specs.SystemType;
        SpecPen.Text = _specs.PenAndTouch;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(_specs.ToCopyText());
        Clipboard.SetContent(package);
    }

    private void SpecsToggle_Click(object sender, RoutedEventArgs e)
    {
        _specsOpen = !_specsOpen;
        SpecsHost.Visibility = _specsOpen ? Visibility.Visible : Visibility.Collapsed;
        SpecsChevron.Glyph = _specsOpen ? "\uE70D" : "\uE70E";
    }

    private static void OpenSystemTool(string fileName)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}
