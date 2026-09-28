using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SalsaNOWSettings.Helpers;
using Windows.UI;

namespace SalsaNOWSettings.Pages;

public sealed partial class ColorsPage : Page
{
    private readonly List<Border> _chips = [];
    private bool _ready;
    private bool _applying;
    private Color _selectedAccent;

    public ColorsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ContentColumn.Width = Math.Clamp(e.NewSize.Width - 96, 360, 800);
    }

    private void Personalization_Click(object sender, RoutedEventArgs e) =>
        SettingsNavigation.Go("personalization");

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildWindowsColors();
        LoadState();
        _ready = true;
    }

    private void LoadState()
    {
        _selectedAccent = WindowsColorSettings.GetAccent();
        ModeBox.SelectedIndex = (int)WindowsColorSettings.GetMode();
        WindowsModeBox.SelectedIndex = WindowsColorSettings.GetSystemUsesLightTheme() ? 0 : 1;
        AppModeBox.SelectedIndex = WindowsColorSettings.GetAppsUseLightTheme() ? 0 : 1;
        TransparencySwitch.IsOn = WindowsColorSettings.GetTransparency();
        AccentSourceBox.SelectedIndex = WindowsColorSettings.GetAutomaticAccent() ? 0 : 1;
        UpdateModeExtras();
        UpdateAccentDetails();
        RebuildRecent();
        RefreshChipSelection();
    }

    private void ModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateModeExtras();
        if (!_ready || ModeBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
        {
            return;
        }

        WindowsColorSettings.SetMode(Enum.Parse<ColorMode>(tag));
    }

    private void WindowsModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready)
        {
            WindowsColorSettings.SetWindowsMode(WindowsModeBox.SelectedIndex == 0);
        }
    }

    private void AppModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready)
        {
            WindowsColorSettings.SetAppMode(AppModeBox.SelectedIndex == 0);
        }
    }

    private void TransparencySwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_ready)
        {
            WindowsColorSettings.SetTransparency(TransparencySwitch.IsOn);
        }
    }

    private void AccentSourceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateAccentDetails();
        if (!_ready)
        {
            return;
        }

        var automatic = AccentSourceBox.SelectedIndex == 0;
        WindowsColorSettings.SetAutomaticAccent(automatic);
        if (automatic)
        {
            _selectedAccent = WindowsColorSettings.DefaultAccent;
            RefreshChipSelection();
        }
    }

    private async void ViewColors_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ColorPicker
        {
            Color = _selectedAccent,
            IsColorChannelTextInputVisible = true,
            IsColorSliderVisible = true,
            IsHexInputVisible = true,
            IsMoreButtonVisible = true
        };

        var dialog = new ContentDialog
        {
            Title = "Custom colors",
            Content = picker,
            PrimaryButtonText = "Done",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ApplyAccent(picker.Color);
        }
    }

    private void UpdateModeExtras()
    {
        var custom = ModeBox.SelectedIndex == 2;
        WindowsModeCard.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        AppModeCard.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateAccentDetails()
    {
        var manual = AccentSourceBox.SelectedIndex == 1;
        AccentDetails.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
        CustomColorsCard.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BuildWindowsColors()
    {
        WindowsColorGrid.ColumnDefinitions.Clear();
        WindowsColorGrid.RowDefinitions.Clear();
        WindowsColorGrid.Children.Clear();
        _chips.Clear();

        for (var i = 0; i < 8; i++)
        {
            WindowsColorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (var i = 0; i < 6; i++)
        {
            WindowsColorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var colors = WindowsColorSettings.WindowsColors;
        for (var i = 0; i < colors.Length; i++)
        {
            var chip = CreateChip(colors[i]);
            Grid.SetRow(chip, i / 8);
            Grid.SetColumn(chip, i % 8);
            WindowsColorGrid.Children.Add(chip);
            _chips.Add(chip);
        }
    }

    private void RebuildRecent()
    {
        RecentHost.Children.Clear();
        foreach (var color in WindowsColorSettings.GetRecentColors())
        {
            RecentHost.Children.Add(CreateChip(color));
        }
    }

    private Border CreateChip(Color color)
    {
        var chip = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(color),
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Tag = color
        };
        chip.PointerPressed += (_, _) => ApplyAccent(color);
        return chip;
    }

    private async void ApplyAccent(Color color)
    {
        if (_applying)
        {
            return;
        }

        _applying = true;
        ColorHost.Height = ColorHost.ActualHeight;
        AccentPalettes.Visibility = Visibility.Collapsed;
        ColorLoading.Visibility = Visibility.Visible;
        ColorLoadingRing.IsActive = true;
        ColorLoading.UpdateLayout();
        try
        {
            await Task.Delay(50);
            _selectedAccent = color;
            WindowsColorSettings.SetAccent(color);
            AccentSourceBox.SelectedIndex = 1;
            UpdateAccentDetails();
            RebuildRecent();
            RefreshChipSelection();
            await Task.Delay(500);
        }
        finally
        {
            ColorLoadingRing.IsActive = false;
            ColorLoading.Visibility = Visibility.Collapsed;
            AccentPalettes.Visibility = Visibility.Visible;
            ColorHost.Height = double.NaN;
            _applying = false;
        }
    }

    private void RefreshChipSelection()
    {
        foreach (var chip in _chips)
        {
            MarkChip(chip, chip.Tag is Color color && WindowsColorSettings.ColorsEqual(color, _selectedAccent));
        }

        foreach (var child in RecentHost.Children)
        {
            if (child is Border chip)
            {
                MarkChip(chip, chip.Tag is Color color && WindowsColorSettings.ColorsEqual(color, _selectedAccent));
            }
        }
    }

    private static void MarkChip(Border chip, bool selected)
    {
        chip.BorderBrush = new SolidColorBrush(selected ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Transparent);
        chip.Child = selected
            ? new FontIcon
            {
                Glyph = "\uE73E",
                FontSize = 12,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
            : null;
    }
}
