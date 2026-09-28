using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SalsaNOWSettings.Helpers;

namespace SalsaNOWSettings.Pages;

public sealed partial class TaskbarPage : Page
{
    private bool _ready;

    public TaskbarPage()
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
        SelectByTag(AlignmentBox, WindowsTaskbarSettings.GetAlignment());
        AutoHideBox.IsChecked = WindowsTaskbarSettings.GetAutoHide();
        BadgesBox.IsChecked = WindowsTaskbarSettings.GetBadges();
        FlashingBox.IsChecked = WindowsTaskbarSettings.GetFlashing();
        TransparencyBox.IsChecked = TranslucentTb.GetEnabled();
        ShareBox.IsChecked = WindowsTaskbarSettings.GetShareWindows();
        ShowDesktopBox.IsChecked = WindowsTaskbarSettings.GetShowDesktopCorner();
        SelectByTag(CombineBox, WindowsTaskbarSettings.GetCombine());
        SelectByTag(SmallerBox, WindowsTaskbarSettings.GetSmallerButtons());
        _ready = true;
    }

    private void AlignmentBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && TryGetTag(AlignmentBox, out var value))
        {
            WindowsTaskbarSettings.SetAlignment(value);
        }
    }

    private void AutoHideBox_Click(object sender, RoutedEventArgs e)
    {
        if (_ready)
        {
            WindowsTaskbarSettings.SetAutoHide(AutoHideBox.IsChecked == true);
        }
    }

    private void BadgesBox_Click(object sender, RoutedEventArgs e)
    {
        if (_ready)
        {
            WindowsTaskbarSettings.SetBadges(BadgesBox.IsChecked == true);
        }
    }

    private void FlashingBox_Click(object sender, RoutedEventArgs e)
    {
        if (_ready)
        {
            WindowsTaskbarSettings.SetFlashing(FlashingBox.IsChecked == true);
        }
    }

    private void TransparencyBox_Click(object sender, RoutedEventArgs e)
    {
        if (_ready)
        {
            TranslucentTb.SetEnabled(TransparencyBox.IsChecked == true);
        }
    }

    private void ShareBox_Click(object sender, RoutedEventArgs e)
    {
        if (_ready)
        {
            WindowsTaskbarSettings.SetShareWindows(ShareBox.IsChecked == true);
        }
    }

    private void ShowDesktopBox_Click(object sender, RoutedEventArgs e)
    {
        if (_ready)
        {
            WindowsTaskbarSettings.SetShowDesktopCorner(ShowDesktopBox.IsChecked == true);
        }
    }

    private void CombineBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && TryGetTag(CombineBox, out var value))
        {
            WindowsTaskbarSettings.SetCombine(value);
        }
    }

    private void SmallerBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && TryGetTag(SmallerBox, out var value))
        {
            WindowsTaskbarSettings.SetSmallerButtons(value);
        }
    }

    private static void SelectByTag(ComboBox box, int value)
    {
        for (var i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is ComboBoxItem item &&
                item.Tag is string tag &&
                int.TryParse(tag, out var parsed) &&
                parsed == value)
            {
                box.SelectedIndex = i;
                return;
            }
        }

        if (box.Items.Count > 0)
        {
            box.SelectedIndex = 0;
        }
    }

    private static bool TryGetTag(ComboBox box, out int value)
    {
        value = 0;
        return box.SelectedItem is ComboBoxItem item &&
               item.Tag is string tag &&
               int.TryParse(tag, out value);
    }
}
