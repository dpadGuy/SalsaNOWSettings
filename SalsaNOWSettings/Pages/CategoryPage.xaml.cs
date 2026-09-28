using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using SalsaNOWSettings.Controls;
using SalsaNOWSettings.Helpers;
using Windows.Storage;
using Windows.UI;

namespace SalsaNOWSettings.Pages;

public sealed partial class CategoryPage : Page
{
    public string CurrentTag { get; private set; } = string.Empty;

    private readonly List<Storyboard> _motion = [];
    private string? _currentWallpaper;

    public CategoryPage()
    {
        InitializeComponent();
        Unloaded += (_, _) => StopMotion();
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ContentColumn.Width = Math.Clamp(e.NewSize.Width - 96, 360, 800);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        CurrentTag = e.Parameter as string ?? "system";
        var section = Catalog.TryGetValue(CurrentTag, out var found)
            ? found
            : Catalog["system"];

        TitleText.Text = section.Title;
        CardsHost.Children.Clear();

        PersonalizationHero.Visibility = CurrentTag == "personalization" ? Visibility.Visible : Visibility.Collapsed;

        if (CurrentTag == "personalization")
        {
            _ = BuildThemePickerAsync();
        }

        for (var i = 0; i < section.Cards.Count; i++)
        {
            var item = section.Cards[i];
            CardsHost.Children.Add(new SettingsCard
            {
                Header = item.Header,
                Description = item.Description,
                IconGlyph = item.IconGlyph,
                NavigateTag = item.NavigateTag,
                ShowChevron = true,
                ShowDivider = i < section.Cards.Count - 1,
                IsClickEnabled = true
            });
        }
    }

    private async Task BuildThemePickerAsync()
    {
        StopMotion();
        ThemeGrid.Children.Clear();
        AppliedHint.Opacity = 0;
        var wallpapers = WindowsWallpaperCatalog.GetThemePreviews();

        await HeroPreview.ShowCurrentAsync();

        if (wallpapers.Count == 0)
        {
            BuildGradientFallback();
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
            var swatch = CreateSwatch(brush, wallpapers[i], image);
            Grid.SetRow(swatch, i / 3);
            Grid.SetColumn(swatch, i % 3);
            ThemeGrid.Children.Add(swatch);
        }

        SelectCurrentThemeTile();
    }

    private void BuildGradientFallback()
    {
        for (var i = 0; i < 6; i++)
        {
            var swatch = CreateSwatch(CreateFallbackBrush(i), null, null);
            Grid.SetRow(swatch, i / 3);
            Grid.SetColumn(swatch, i % 3);
            ThemeGrid.Children.Add(swatch);
        }
    }

    private Border CreateSwatch(Brush background, string? path, ImageSource? preview)
    {
        var swatch = WallpaperPreview.CreateTile(background, new SwatchData(path, preview));
        swatch.PointerPressed += Swatch_PointerPressed;
        return swatch;
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
        if (sender is Border swatch)
        {
            ShowPreview(swatch, applyDesktop: true);
        }
    }

    private void ShowPreview(Border swatch, bool applyDesktop)
    {
        WallpaperPreview.SelectTile(ThemeGrid.Children, swatch);

        if (swatch.Tag is not SwatchData data)
        {
            return;
        }

        HeroPreview.FadeTo(data.Preview, swatch.Background);

        if (applyDesktop && data.Path is not null && DesktopWallpaper.Apply(data.Path))
        {
            _currentWallpaper = data.Path;
            PulseHint();
        }
    }

    private void PulseHint()
    {
        AppliedHint.Opacity = 0;
        var show = Animate(AppliedHint, "Opacity", 0, 1, TimeSpan.FromMilliseconds(220), new CubicEase { EasingMode = EasingMode.EaseOut });
        var hide = Animate(AppliedHint, "Opacity", 1, 0, TimeSpan.FromMilliseconds(400), new CubicEase { EasingMode = EasingMode.EaseIn }, TimeSpan.FromSeconds(2.2));
        Track(show);
        Track(hide);
    }

    private Storyboard Animate(DependencyObject target, string property, double from, double to, TimeSpan duration, EasingFunctionBase ease, TimeSpan? delay = null, bool forever = false)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = duration,
            EasingFunction = ease,
            AutoReverse = forever,
            RepeatBehavior = forever ? RepeatBehavior.Forever : new RepeatBehavior(1),
            BeginTime = delay ?? TimeSpan.Zero,
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        var board = new Storyboard();
        board.Children.Add(animation);
        board.Begin();
        return board;
    }

    private void Track(Storyboard board) => _motion.Add(board);

    private void StopMotion()
    {
        foreach (var board in _motion)
        {
            board.Stop();
        }

        _motion.Clear();
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

    private sealed record SwatchData(string? Path, ImageSource? Preview);

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

    private static readonly Dictionary<string, Section> Catalog = new()
    {
        ["system"] = new(
            "System",
            [
                new("Persistent storage", "Storage space, cleanup recommendations", "\uEDA2", "storage"),
                new("About", "Rig specifications, Windows information", "\uE946", "about"),
            ]),
        ["personalization"] = new(
            "Personalization",
            [
                new("Background", "Background image, color, slideshow", "\uE771", "background"),
                new("Colors", "Accent color, transparency effects, color theme", "\uE790", "colors"),
                new("Taskbar", "Taskbar behavior, system pins", "\uE8A9", "taskbar"),
            ]),
        ["apps"] = new(
            "Apps",
            [
                new("Default apps", "Web browser, email, file type defaults", "\uE8A5", "defaults"),
                new("Startup", "Apps that load when this environment starts", "\uE7E8", "startup"),
            ]),
    };

    private sealed record Section(string Title, IReadOnlyList<CardItem> Cards);

    private sealed record CardItem(string Header, string Description, string IconGlyph, string NavigateTag = "");
}
