using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SalsaNOWSettings.Controls;

namespace SalsaNOWSettings.Pages;

public sealed partial class ControlsPage : Page
{
    public ControlsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateReadout();
    }

    private void Knob_ValueChanged(object? sender, double value) => UpdateReadout();

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        GainKnob.Value = 62;
        ColorKnob.Value = 28;
        DriveKnob.Value = 44;
    }

    private void UpdateReadout()
    {
        if (ReadoutText is null || GainKnob is null || ColorKnob is null || DriveKnob is null)
        {
            return;
        }

        ReadoutText.Text =
            $"Gain {GainKnob.Value:0}  ·  Color {ColorKnob.Value:0}  ·  Drive {DriveKnob.Value:0}";
    }
}
