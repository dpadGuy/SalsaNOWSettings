using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using SalsaNOWSettings.Helpers;
using Windows.Storage;

namespace SalsaNOWSettings.Controls;

public sealed partial class WallpaperPreview : UserControl
{
    private ImageSource? _pendingPreview;
    private Brush? _pendingBrush;
    private bool _fadingOut;

    public WallpaperPreview()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ShowCurrentAsync();
    }

    public async Task ShowCurrentAsync()
    {
        var path = DesktopWallpaper.GetCurrentPath();
        if (path is null)
        {
            Show(new SolidColorBrush(Microsoft.UI.Colors.Black));
            return;
        }

        var image = await LoadBitmapAsync(path, 960);
        if (image is null)
        {
            Show(new SolidColorBrush(Microsoft.UI.Colors.Black));
            return;
        }

        Show(new ImageBrush
        {
            ImageSource = image,
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        });
    }

    public void Show(Brush? brush)
    {
        PreviewLayer.Background = brush ?? new SolidColorBrush(Microsoft.UI.Colors.Black);
        PreviewLayer.Opacity = 1;
    }

    public void FadeTo(ImageSource? source, Brush? brush)
    {
        _pendingPreview = source;
        _pendingBrush = brush;

        if (PreviewLayer.Background is null)
        {
            ApplyPending();
            PreviewLayer.Opacity = 1;
            return;
        }

        if (_fadingOut)
        {
            return;
        }

        if (PreviewLayer.Opacity > 0.95 &&
            PreviewLayer.Background is ImageBrush current &&
            ReferenceEquals(current.ImageSource, source))
        {
            return;
        }

        _fadingOut = true;
        var fadeOut = CreateFade(PreviewLayer.Opacity, 0, TimeSpan.FromMilliseconds(320), EasingMode.EaseIn);
        fadeOut.Completed += (_, _) =>
        {
            ApplyPending();
            _fadingOut = false;
            CreateFade(0, 1, TimeSpan.FromMilliseconds(400), EasingMode.EaseOut).Begin();
        };
        fadeOut.Begin();
    }

    public static Border CreateTile(Brush background, object? tag, string styleKey = "WallpaperTileStyle")
    {
        return new Border
        {
            Tag = tag,
            Background = background,
            Style = Application.Current.Resources[styleKey] as Style
        };
    }

    public static void SelectTile(UIElementCollection tiles, Border selected)
    {
        foreach (var child in tiles)
        {
            if (child is Border tile)
            {
                tile.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
        }

        selected.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
    }

    public static void SelectTile(IEnumerable<Border> tiles, Border selected)
    {
        foreach (var tile in tiles)
        {
            tile.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        selected.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
    }

    private void ApplyPending()
    {
        if (_pendingPreview is not null)
        {
            PreviewLayer.Background = new ImageBrush
            {
                ImageSource = _pendingPreview,
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
            return;
        }

        if (_pendingBrush is not null)
        {
            PreviewLayer.Background = _pendingBrush;
        }
    }

    private Storyboard CreateFade(double from, double to, TimeSpan duration, EasingMode mode)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = duration,
            EasingFunction = new SineEase { EasingMode = mode },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, PreviewLayer);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var board = new Storyboard();
        board.Children.Add(animation);
        return board;
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
