using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace SalsaNOWSettings.Controls;

public sealed partial class SettingsCard : UserControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionContentProperty =
        DependencyProperty.Register(nameof(ActionContent), typeof(object), typeof(SettingsCard), new PropertyMetadata(null));

    public static readonly DependencyProperty ShowChevronProperty =
        DependencyProperty.Register(nameof(ShowChevron), typeof(bool), typeof(SettingsCard), new PropertyMetadata(true));

    public static readonly DependencyProperty ShowDividerProperty =
        DependencyProperty.Register(nameof(ShowDivider), typeof(bool), typeof(SettingsCard), new PropertyMetadata(true));

    public static readonly DependencyProperty IsClickEnabledProperty =
        DependencyProperty.Register(nameof(IsClickEnabled), typeof(bool), typeof(SettingsCard), new PropertyMetadata(true));

    public static readonly DependencyProperty NavigateTagProperty =
        DependencyProperty.Register(nameof(NavigateTag), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public event RoutedEventHandler? Click;

    public SettingsCard()
    {
        InitializeComponent();
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public bool ShowChevron
    {
        get => (bool)GetValue(ShowChevronProperty);
        set => SetValue(ShowChevronProperty, value);
    }

    public bool ShowDivider
    {
        get => (bool)GetValue(ShowDividerProperty);
        set => SetValue(ShowDividerProperty, value);
    }

    public bool IsClickEnabled
    {
        get => (bool)GetValue(IsClickEnabledProperty);
        set => SetValue(IsClickEnabledProperty, value);
    }

    public string NavigateTag
    {
        get => (string)GetValue(NavigateTagProperty);
        set => SetValue(NavigateTagProperty, value);
    }

    public Visibility DescriptionVisibility(string? description) =>
        string.IsNullOrWhiteSpace(description) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility IconVisibility(string? glyph) =>
        string.IsNullOrWhiteSpace(glyph) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ChevronVisibility(bool show) =>
        show ? Visibility.Visible : Visibility.Collapsed;

    private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        RootGrid.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];

    private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e) =>
        RootGrid.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (IsClickEnabled)
        {
            RootGrid.Background = (Brush)Application.Current.Resources["SubtleFillColorTertiaryBrush"];
        }
    }

    private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e) =>
        RootGrid.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];

    private void RootGrid_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (!IsClickEnabled)
        {
            return;
        }

        if (ActionPresenter.IsDescendantOfSource(e.OriginalSource))
        {
            return;
        }

        Click?.Invoke(this, new RoutedEventArgs());

        if (!string.IsNullOrWhiteSpace(NavigateTag))
        {
            Helpers.SettingsNavigation.Go(NavigateTag);
        }
    }
}

internal static class VisualTreeExtensions
{
    public static bool IsDescendantOfSource(this FrameworkElement root, object? source)
    {
        if (source is not DependencyObject current)
        {
            return false;
        }

        while (current is not null)
        {
            if (ReferenceEquals(current, root))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
