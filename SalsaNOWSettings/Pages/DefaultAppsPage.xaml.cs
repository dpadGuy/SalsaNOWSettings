using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SalsaNOWSettings.Controls;
using SalsaNOWSettings.Helpers;

namespace SalsaNOWSettings.Pages;

public sealed partial class DefaultAppsPage : Page
{
    private static readonly DefaultSection[] CommonSections =
    [
        new("Archiving", "Compressed folders and archives", "\uE7B8", [".zip", ".rar", ".7z", ".tar"]),
        new("Text files", "Notes, logs, and config files", "\uE8A5", [".txt", ".log", ".ini", ".json", ".md"]),
        new("Photos", "Image files", "\uE91B", [".jpg", ".jpeg", ".png", ".webp"]),
        new("Videos", "Video files", "\uE714", [".mp4", ".mkv", ".webm", ".avi", ".mov", ".wmv", ".m4v", ".flv"]),
        new("Music", "Audio files", "\uE8D6", [".mp3", ".flac", ".wav"]),
    ];

    private IReadOnlyList<AssociationInfo> _catalog = [];

    public DefaultAppsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ContentColumn.Width = Math.Clamp(e.NewSize.Width - 96, 360, 800);
    }

    private void Apps_Click(object sender, RoutedEventArgs e) =>
        SettingsNavigation.Go("apps");

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _catalog = await Task.Run(WindowsDefaultApps.LoadCatalog);
        ApplyFilter(TypeSearch.Text);
        RebuildSections();
    }

    private void TypeSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ApplyFilter(sender.Text);
        }
    }

    private void TypeSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var typed = args.QueryText;
        var matches = WindowsDefaultApps.Search(_catalog, typed);
        _ = ChooseAsync(matches.Count == 1 ? matches[0].Type : WindowsDefaultApps.Normalize(typed));
    }

    private void ApplyFilter(string? query)
    {
        ResultsHost.Children.Clear();
        if (string.IsNullOrWhiteSpace(query))
        {
            ResultsChrome.Visibility = Visibility.Collapsed;
            return;
        }

        var matches = WindowsDefaultApps.Search(_catalog, query);
        if (matches.Count == 0)
        {
            var typed = WindowsDefaultApps.Normalize(query);
            if (string.IsNullOrWhiteSpace(typed))
            {
                ResultsChrome.Visibility = Visibility.Collapsed;
                return;
            }

            ResultsHost.Children.Add(CreateTypeCard(
                WindowsDefaultApps.Describe(new AssociationInfo(typed, !typed.StartsWith('.'), string.Empty)),
                false));
            ResultsChrome.Visibility = Visibility.Visible;
            return;
        }

        for (var i = 0; i < matches.Count; i++)
        {
            ResultsHost.Children.Add(CreateTypeCard(matches[i], i < matches.Count - 1));
        }

        ResultsChrome.Visibility = Visibility.Visible;
    }

    private SettingsCard CreateTypeCard(AssociationInfo item, bool divider, string? icon = null)
    {
        var empty = string.IsNullOrWhiteSpace(item.CurrentApp);
        var card = new SettingsCard
        {
            Header = item.Type,
            Description = empty ? "Choose a default" : item.CurrentApp,
            IconGlyph = icon ?? (empty ? "\uE710" : "\uE8A5"),
            ShowChevron = false,
            ShowDivider = divider,
            ActionContent = CreateOpenIcon(item.Type)
        };
        card.Click += (_, _) => _ = ChooseAsync(item.Type);
        return card;
    }

    private FontIcon CreateOpenIcon(string? type)
    {
        var icon = new FontIcon
        {
            FontSize = 14,
            Glyph = "\uE8A7",
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        icon.Tapped += (_, _) => _ = ChooseAsync(type);
        return icon;
    }

    private FontIcon CreateBrowserOpenIcon()
    {
        var icon = new FontIcon
        {
            FontSize = 14,
            Glyph = "\uE8A7",
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        };
        icon.Tapped += (_, _) => _ = ChooseBrowserAsync();
        return icon;
    }

    private bool _choosing;

    private async Task ChooseAsync(string? type)
    {
        if (_choosing)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            TypeSearch.Focus(FocusState.Programmatic);
            return;
        }

        _choosing = true;
        try
        {
            var application = await FileExplorerPicker.PickFileAsync(
                [".exe"],
                "Choose a default app",
                WindowsDefaultApps.PreferredAppFolder);
            if (string.IsNullOrWhiteSpace(application))
            {
                return;
            }

            if (WindowsDefaultApps.TryApply(type, application, out var error))
            {
                ClearError();
            }
            else
            {
                ShowError(error);
            }

            ApplyFilter(TypeSearch.Text);
            RebuildSections();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _choosing = false;
        }
    }

    private async Task ChooseAllAsync(string[] types)
    {
        if (_choosing || types.Length == 0)
        {
            return;
        }

        _choosing = true;
        try
        {
            var application = await FileExplorerPicker.PickFileAsync(
                [".exe"],
                "Replace all with one app",
                WindowsDefaultApps.PreferredAppFolder);
            if (string.IsNullOrWhiteSpace(application))
            {
                return;
            }

            string? error = null;
            foreach (var type in types)
            {
                if (!WindowsDefaultApps.TryApply(type, application, out var applyError))
                {
                    error = applyError;
                    break;
                }
            }

            if (error is null)
            {
                ClearError();
            }
            else
            {
                ShowError(error);
            }

            ApplyFilter(TypeSearch.Text);
            RebuildSections();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _choosing = false;
        }
    }

    private async Task ChooseBrowserAsync()
    {
        if (_choosing)
        {
            return;
        }

        _choosing = true;
        try
        {
            var application = await FileExplorerPicker.PickFileAsync(
                [".exe"],
                "Choose a default browser",
                WindowsDefaultApps.PreferredAppFolder);
            if (string.IsNullOrWhiteSpace(application))
            {
                return;
            }

            if (WindowsDefaultApps.TrySetDefaultBrowser(application, out var error))
            {
                ClearError();
            }
            else
            {
                ShowError(error);
            }

            RebuildSections();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _choosing = false;
        }
    }

    private void RebuildSections()
    {
        SectionsHost.Children.Clear();
        SectionsHost.Children.Add(CreateBrowserSection());
        foreach (var section in CommonSections)
        {
            var types = section.Types;
            var block = new StackPanel { Spacing = 8 };
            block.Children.Add(CreateSectionHeader(section.Title, () => _ = ChooseAllAsync(types)));
            block.Children.Add(new TextBlock
            {
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Text = section.Description
            });

            var cards = new StackPanel();
            for (var i = 0; i < section.Types.Length; i++)
            {
                var type = section.Types[i];
                var info = WindowsDefaultApps.Describe(new AssociationInfo(type, !type.StartsWith('.'), string.Empty));
                cards.Children.Add(CreateTypeCard(info, i < section.Types.Length - 1, section.Icon));
            }

            block.Children.Add(new Border
            {
                Style = (Style)Application.Current.Resources["SettingsChromeStyle"],
                Child = cards
            });
            SectionsHost.Children.Add(block);
        }
    }

    private StackPanel CreateBrowserSection()
    {
        var name = WindowsDefaultApps.GetDefaultBrowserName();
        var path = WindowsDefaultApps.GetDefaultBrowser();
        var empty = string.IsNullOrWhiteSpace(path);
        var card = new SettingsCard
        {
            Header = empty ? "Choose a default browser" : name,
            Description = empty ? "Pick the browser .exe to use, current default browser is Waterfox." : path,
            IconGlyph = "\uE774",
            ShowChevron = false,
            ShowDivider = false,
            ActionContent = CreateBrowserOpenIcon()
        };
        card.Click += (_, _) => _ = ChooseBrowserAsync();

        var block = new StackPanel { Spacing = 8 };
        block.Children.Add(CreateSectionHeader("Web browser"));
        block.Children.Add(new TextBlock
        {
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Text = "Links that usually open in a browser"
        });
        block.Children.Add(new Border
        {
            Style = (Style)Application.Current.Resources["SettingsChromeStyle"],
            Child = card
        });
        return block;
    }

    private static Grid CreateSectionHeader(string title, Action? replaceAll = null)
    {
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = new TextBlock
        {
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = title,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(titleBlock);

        if (replaceAll is not null)
        {
            var button = new Button
            {
                Content = "Replace all",
                MinWidth = 0,
                Padding = new Thickness(12, 6, 12, 6),
                VerticalAlignment = VerticalAlignment.Center
            };
            button.Click += (_, _) => replaceAll();
            Grid.SetColumn(button, 1);
            header.Children.Add(button);
        }

        return header;
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

    private sealed record DefaultSection(string Title, string Description, string Icon, string[] Types);
}
