using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SalsaNOWSettings.Controls;
using SalsaNOWSettings.Helpers;
using Windows.Storage;

namespace SalsaNOWSettings.Pages;

public sealed partial class BackgroundPage : Page
{
    private readonly List<Border> _tiles = [];
    private bool _ready;

    public BackgroundPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ContentColumn.Width = Math.Clamp(e.NewSize.Width - 96, 360, 960);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        BingSwitch.IsOn = BingWallpaper.IsEnabled();
        _ready = true;
        await LoadWallpapersAsync();
    }

    private void Personalization_Click(object sender, RoutedEventArgs e) =>
        Helpers.SettingsNavigation.Go("personalization");

    private async void BingSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        if (!BingSwitch.IsOn)
        {
            BingWallpaper.SetEnabled(false);
            ClearError();
            return;
        }

        BingSwitch.IsEnabled = false;
        try
        {
            var (ok, error) = await BingWallpaper.ApplyTodayAsync();
            if (!ok)
            {
                _ready = false;
                BingSwitch.IsOn = false;
                _ready = true;
                ShowError(error ?? "Couldn't apply today's Bing wallpaper.");
                return;
            }

            ClearError();
            await LoadWallpapersAsync();
        }
        finally
        {
            BingSwitch.IsEnabled = true;
        }
    }

    private async Task LoadWallpapersAsync()
    {
        WallpaperGrid.Items.Clear();
        _tiles.Clear();
        WallpaperGrid.Visibility = Visibility.Collapsed;
        WallpaperLoading.Visibility = Visibility.Visible;

        try
        {
            var current = DesktopWallpaper.GetSourcePath();
            var paths = WindowsWallpaperCatalog.GetAllWallpapers();
            Border? match = null;

            await HeroPreview.ShowCurrentAsync();

            foreach (var path in paths)
            {
                var image = await LoadBitmapAsync(path, 480);
                Brush brush = image is null
                    ? new SolidColorBrush(Microsoft.UI.Colors.DimGray)
                    : new ImageBrush
                    {
                        ImageSource = image,
                        Stretch = Stretch.UniformToFill,
                        AlignmentX = AlignmentX.Center,
                        AlignmentY = AlignmentY.Center
                    };
                var tile = WallpaperPreview.CreateTile(brush, path, "WallpaperRecentTileStyle");
                if (DesktopWallpaper.Matches(path, current))
                {
                    match = tile;
                }
                _tiles.Add(tile);
                WallpaperGrid.Items.Add(tile);
            }

            if (match is not null)
            {
                WallpaperPreview.SelectTile(_tiles, match);
            }
        }
        finally
        {
            WallpaperLoading.Visibility = Visibility.Collapsed;
            WallpaperGrid.Visibility = Visibility.Visible;
        }
    }

    private void WallpaperGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not Border tile || tile.Tag is not string path)
        {
            return;
        }

        WallpaperPreview.SelectTile(_tiles, tile);
        var source = tile.Background is ImageBrush imageBrush ? imageBrush.ImageSource : null;
        HeroPreview.FadeTo(source, tile.Background);
        DesktopWallpaper.Apply(path);
        SetBingOff();
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileExplorerPicker.PickFileAsync(
            [".jpg", ".jpeg", ".png", ".bmp"],
            "Choose a photo",
            KnownExplorerFolders.Pictures);
        if (path is null)
        {
            return;
        }

        var image = await LoadBitmapAsync(path, 640);
        Brush? brush = image is null
            ? null
            : new ImageBrush
            {
                ImageSource = image,
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
        HeroPreview.FadeTo(image, brush);
        DesktopWallpaper.Apply(path);
        SetBingOff();
        await LoadWallpapersAsync();
    }

    private void SetBingOff()
    {
        if (!BingSwitch.IsOn)
        {
            return;
        }

        _ready = false;
        BingSwitch.IsOn = false;
        _ready = true;
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
}
