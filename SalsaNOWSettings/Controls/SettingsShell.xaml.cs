using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using SalsaNOWSettings.Helpers;
using SalsaNOWSettings.Pages;

namespace SalsaNOWSettings.Controls;

public sealed partial class SettingsShell : UserControl
{
    public static SettingsShell? Current { get; private set; }

    public string VersionLabel => AppVersion.Display;

    public static readonly DependencyProperty UserDisplayNameProperty =
        DependencyProperty.Register(nameof(UserDisplayName), typeof(string), typeof(SettingsShell), new PropertyMetadata(Environment.UserName));

    public static readonly DependencyProperty UserSubtitleProperty =
        DependencyProperty.Register(nameof(UserSubtitle), typeof(string), typeof(SettingsShell), new PropertyMetadata("Local account"));

    public static readonly DependencyProperty EnvironmentNameProperty =
        DependencyProperty.Register(nameof(EnvironmentName), typeof(string), typeof(SettingsShell), new PropertyMetadata(Environment.MachineName));

    public static readonly DependencyProperty EnvironmentDetailsProperty =
        DependencyProperty.Register(nameof(EnvironmentDetails), typeof(string), typeof(SettingsShell), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShowTitleBarProperty =
        DependencyProperty.Register(nameof(ShowTitleBar), typeof(bool), typeof(SettingsShell), new PropertyMetadata(true));

    private static readonly IReadOnlyList<SearchHit> SearchCatalog =
    [
        new("home", "Home", "Start page, recommended settings"),
        new("system", "System", "Display, sound, power, knobs"),
        new("personalization", "Personalization", "Background, colors, themes"),
        new("background", "Background", "Wallpaper, pictures, desktop fit, Bing wallpaper of the day"),
        new("colors", "Colors", "Accent color, transparency, light and dark"),
        new("taskbar", "Taskbar", "Taskbar alignment, badges, hide, multiple displays"),
        new("about", "About", "Device specifications, rename this PC"),
        new("storage", "Storage", "I: drive, disk usage, largest files, cleanup"),
        new("apps", "Apps", "Installed apps, defaults"),
        new("defaults", "Default apps", "File type defaults, link types, choose a default"),
        new("startup", "Startup", "Startup batch, Steam silent launch, Steam Input, GeForce NOW session start, StartupBatch.bat"),
    ];

    public SettingsShell()
    {
        InitializeComponent();
        Current = this;
        ApplyCurrentAccount();
        SettingsNavigation.Requested += NavigateTo;
        Unloaded += OnUnloaded;
    }

    private void ApplyCurrentAccount()
    {
        var account = WindowsAccount.Current;
        UserDisplayName = account.DisplayName;
        UserSubtitle = account.Subtitle;
        if (string.IsNullOrWhiteSpace(account.PicturePath))
        {
            return;
        }

        try
        {
            AccountPicture.ProfilePicture = new BitmapImage(new Uri(account.PicturePath));
        }
        catch
        {
        }
    }

    public FrameworkElement TitleBar => AppTitleBar;

    public Panel OverlayHost => PageHost;

    public string UserDisplayName
    {
        get => (string)GetValue(UserDisplayNameProperty);
        set => SetValue(UserDisplayNameProperty, value);
    }

    public string UserSubtitle
    {
        get => (string)GetValue(UserSubtitleProperty);
        set => SetValue(UserSubtitleProperty, value);
    }

    public string EnvironmentName
    {
        get => (string)GetValue(EnvironmentNameProperty);
        set => SetValue(EnvironmentNameProperty, value);
    }

    public string EnvironmentDetails
    {
        get => (string)GetValue(EnvironmentDetailsProperty);
        set => SetValue(EnvironmentDetailsProperty, value);
    }

    public bool ShowTitleBar
    {
        get => (bool)GetValue(ShowTitleBarProperty);
        set => SetValue(ShowTitleBarProperty, value);
    }

    public Visibility TitleBarVisibility(bool show) =>
        show ? Visibility.Visible : Visibility.Collapsed;

    public void NavigateTo(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            tag = "home";
        }

        var pageType = tag switch
        {
            "home" => typeof(HomePage),
            "controls" => typeof(ControlsPage),
            "background" => typeof(BackgroundPage),
            "colors" => typeof(ColorsPage),
            "taskbar" => typeof(TaskbarPage),
            "about" => typeof(AboutPage),
            "storage" => typeof(StoragePage),
            "startup" => typeof(StartupPage),
            "defaults" => typeof(DefaultAppsPage),
            _ => typeof(CategoryPage)
        };

        if (pageType == typeof(CategoryPage) &&
            ContentFrame.Content is CategoryPage current &&
            string.Equals(current.CurrentTag, tag, StringComparison.OrdinalIgnoreCase))
        {
            SelectNavItem(tag);
            return;
        }

        ContentFrame.Navigate(pageType, tag);
        SelectNavItem(tag switch
        {
            "background" or "colors" or "taskbar" => "personalization",
            "about" or "storage" => "system",
            "startup" or "defaults" => "apps",
            _ => tag
        });
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e) => NavigateTo("home");

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void SelectNavItem(string tag)
    {
        foreach (var raw in NavView.MenuItems)
        {
            if (raw is NavigationViewItem item && item.Tag is string itemTag &&
                string.Equals(itemTag, tag, StringComparison.OrdinalIgnoreCase))
            {
                NavView.SelectedItem = item;
                return;
            }
        }
    }

    public void HandleSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) =>
        SearchBox_TextChanged(sender, args);

    public void HandleSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) =>
        SearchBox_QuerySubmitted(sender, args);

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var query = sender.Text?.Trim() ?? string.Empty;
        sender.ItemsSource = string.IsNullOrEmpty(query)
            ? Array.Empty<string>()
            : SearchCatalog
                .Where(hit => hit.Matches(query))
                .Select(hit => hit.Title)
                .ToList();
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var query = args.ChosenSuggestion as string ?? args.QueryText;
        var hit = SearchCatalog.FirstOrDefault(item => item.Matches(query));
        if (hit is not null)
        {
            NavigateTo(hit.Tag);
            sender.Text = string.Empty;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SettingsNavigation.Requested -= NavigateTo;
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }

    private sealed record SearchHit(string Tag, string Title, string Keywords)
    {
        public bool Matches(string query) =>
            Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            Keywords.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            Tag.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
