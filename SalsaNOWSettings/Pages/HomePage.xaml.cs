using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SalsaNOWSettings.Controls;
using SalsaNOWSettings.Helpers;
using Windows.Storage;
using Windows.UI;

namespace SalsaNOWSettings.Pages;

public sealed partial class HomePage : Page
{
    private bool _ready;

    public HomePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var shell = SettingsShell.Current;
        DeviceNameText.Text = string.IsNullOrWhiteSpace(shell?.EnvironmentName)
            ? Environment.MachineName
            : shell!.EnvironmentName;

        DeviceDetailsText.Text = string.IsNullOrWhiteSpace(shell?.EnvironmentDetails)
            ? $"{RuntimeInformation.OSDescription}  ·  {Environment.ProcessorCount} cores"
            : shell!.EnvironmentDetails;

        var specs = DeviceSpecs.Load();
        InfoProcessor.Text = specs.Processor;
        InfoProcessorSpeed.Text = specs.FormatProcessorSpeedLabel();
        InfoProcessorSpeed.Visibility = string.IsNullOrEmpty(InfoProcessorSpeed.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
        InfoRam.Text = specs.InstalledRam;
        InfoRamUsable.Text = specs.FormatUsableRamLabel();
        InfoRamUsable.Visibility = string.IsNullOrEmpty(InfoRamUsable.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
        InfoEdition.Text = specs.Edition;
        InfoOsBuild.Text = specs.OsBuild;
        ApplyNarrowLayout(ActualWidth < 920);
        LoadColorMode();
        await BuildThemePickerAsync();
        _ready = true;
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        PageRoot.Width = Math.Clamp(e.NewSize.Width - 96, 360, 1040);
        ApplyNarrowLayout(e.NewSize.Width < 920);
    }

    private void ApplyNarrowLayout(bool narrow)
    {
        EnsureCardRows(narrow ? 2 : 1);

        if (narrow)
        {
            if (CardsGrid.ColumnDefinitions.Count != 1)
            {
                CardsGrid.ColumnDefinitions.Clear();
                CardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            Grid.SetColumn(PersonalizeBlock, 0);
            Grid.SetRow(PersonalizeBlock, 1);

            if (DeviceHeader.RowDefinitions.Count != 2)
            {
                DeviceHeader.RowDefinitions.Clear();
                DeviceHeader.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                DeviceHeader.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
        }
        else
        {
            if (CardsGrid.ColumnDefinitions.Count != 2)
            {
                CardsGrid.ColumnDefinitions.Clear();
                CardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                CardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            Grid.SetRow(PersonalizeBlock, 0);
            Grid.SetColumn(PersonalizeBlock, 1);
            DeviceHeader.RowDefinitions.Clear();
        }
    }

    private void EnsureCardRows(int count)
    {
        while (CardsGrid.RowDefinitions.Count < count)
        {
            CardsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        var box = new TextBox { Text = DeviceNameText.Text };
        var dialog = new ContentDialog
        {
            Title = "Rename this environment",
            Content = box,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary &&
            !string.IsNullOrWhiteSpace(box.Text) &&
            SettingsShell.Current is { } shell)
        {
            shell.EnvironmentName = box.Text.Trim();
            DeviceNameText.Text = shell.EnvironmentName;
        }
    }

    private async Task BuildThemePickerAsync()
    {
        ThemeGrid.Children.Clear();
        var wallpapers = WindowsWallpaperCatalog.GetThemePreviews();
        await HeroPreview.ShowCurrentAsync();

        if (wallpapers.Count == 0)
        {
            for (var i = 0; i < 6; i++)
            {
                AddSwatch(CreateFallbackBrush(i), null, null, i);
            }

            return;
        }

        for (var i = 0; i < wallpapers.Count && i < 6; i++)
        {
            var image = await LoadBitmapAsync(wallpapers[i], 640);
            Brush brush = image is null
                ? CreateFallbackBrush(i)
                : new ImageBrush
                {
                    ImageSource = image,
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };
            AddSwatch(brush, wallpapers[i], image, i);
        }

        SelectCurrentThemeTile();
    }

    private void AddSwatch(Brush background, string? path, ImageSource? preview, int index)
    {
        var swatch = WallpaperPreview.CreateTile(background, new SwatchData(path, preview), "WallpaperHomeTileStyle");
        swatch.PointerPressed += Swatch_PointerPressed;
        Grid.SetRow(swatch, index / 3);
        Grid.SetColumn(swatch, index % 3);
        ThemeGrid.Children.Add(swatch);
    }

    private void SelectCurrentThemeTile()
    {
        var current = DesktopWallpaper.GetCurrentPath();
        foreach (var child in ThemeGrid.Children)
        {
            if (child is Border tile &&
                tile.Tag is SwatchData data &&
                DesktopWallpaper.PathsEqual(data.Path, current))
            {
                WallpaperPreview.SelectTile(ThemeGrid.Children, tile);
                return;
            }
        }
    }

    private void Swatch_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border swatch || swatch.Tag is not SwatchData data)
        {
            return;
        }

        WallpaperPreview.SelectTile(ThemeGrid.Children, swatch);
        HeroPreview.FadeTo(data.Preview, swatch.Background);
        if (data.Path is not null)
        {
            DesktopWallpaper.Apply(data.Path);
        }
    }

    private void LoadColorMode()
    {
        ModeBox.SelectedIndex = (int)WindowsColorSettings.GetMode();
        WindowsModeBox.SelectedIndex = WindowsColorSettings.GetSystemUsesLightTheme() ? 0 : 1;
        AppModeBox.SelectedIndex = WindowsColorSettings.GetAppsUseLightTheme() ? 0 : 1;
        UpdateModeExtras();
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

    private void UpdateModeExtras()
    {
        var custom = ModeBox.SelectedIndex == 2;
        WindowsModeCard.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        AppModeCard.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Brush CreateFallbackBrush(int index)
    {
        var ends = new Color[]
        {
            Color.FromArgb(255, 74, 163, 217),
            Color.FromArgb(255, 96, 165, 250),
            Color.FromArgb(255, 51, 65, 85),
            Color.FromArgb(255, 245, 158, 11),
            Color.FromArgb(255, 244, 114, 182),
            Color.FromArgb(255, 52, 211, 153),
        };
        var starts = new Color[]
        {
            Color.FromArgb(255, 11, 31, 74),
            Color.FromArgb(255, 17, 24, 39),
            Color.FromArgb(255, 15, 23, 42),
            Color.FromArgb(255, 124, 45, 18),
            Color.FromArgb(255, 76, 29, 49),
            Color.FromArgb(255, 6, 78, 59),
        };

        return new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = starts[index % starts.Length], Offset = 0 },
                new GradientStop { Color = ends[index % ends.Length], Offset = 1 }
            }
        };
    }

    private static async Task<BitmapImage?> LoadBitmapAsync(string path, int decodeWidth)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenReadAsync();
            var image = new BitmapImage { DecodePixelWidth = decodeWidth };
            await image.SetSourceAsync(stream);
            return image;
        }
        catch
        {
            return null;
        }
    }

    private sealed record SwatchData(string? Path, ImageSource? Preview);
}
