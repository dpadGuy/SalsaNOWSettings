using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SalsaNOWSettings.Helpers;
using Windows.System;

namespace SalsaNOWSettings.Pages;

public sealed partial class StoragePage : Page
{
    private string _folder = DriveUsage.RootPath;
    private readonly List<string> _back = [];
    private readonly List<string> _forward = [];
    private int _scanId;
    private long _folderTotal;
    private bool _busy;

    public StoragePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ContentColumn.Width = Math.Clamp(e.NewSize.Width - 96, 360, 800);
    }

    private void System_Click(object sender, RoutedEventArgs e) =>
        SettingsNavigation.Go("system");

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        _ = LoadAsync(DriveUsage.RootPath, recordHistory: false);

    private void Refresh_Click(object sender, RoutedEventArgs e) =>
        _ = LoadAsync(_folder, recordHistory: false);

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_back.Count == 0)
        {
            return;
        }

        _forward.Add(_folder);
        var previous = _back[^1];
        _back.RemoveAt(_back.Count - 1);
        _ = LoadAsync(previous, recordHistory: false);
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_forward.Count == 0)
        {
            return;
        }

        _back.Add(_folder);
        var next = _forward[^1];
        _forward.RemoveAt(_forward.Count - 1);
        _ = LoadAsync(next, recordHistory: false);
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        var parent = DriveUsage.ParentPath(_folder);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            _ = LoadAsync(parent, recordHistory: true);
        }
    }

    private void AddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        if (DriveUsage.TryResolveFolder(AddressBox.Text, out var folder))
        {
            _ = LoadAsync(folder, recordHistory: true);
            return;
        }

        AddressBox.Text = _folder;
        ShowError("Enter a folder on I:.");
    }

    private async Task LoadAsync(string path, bool recordHistory)
    {
        if (!DriveUsage.TryResolveFolder(path, out var folder))
        {
            ShowError("That folder is not on I:.");
            AddressBox.Text = _folder;
            return;
        }

        if (recordHistory && !PathsEqual(_folder, folder))
        {
            _back.Add(_folder);
            _forward.Clear();
        }

        var id = Interlocked.Increment(ref _scanId);
        _folder = folder;
        _busy = true;
        AddressBox.Text = folder;
        UpdateChrome();
        ScanRing.IsActive = true;
        ScanStatus.Text = "Scanning " + folder;
        ItemsHost.Children.Clear();
        RefreshDriveCard();

        IReadOnlyList<StorageEntry> items;
        try
        {
            items = await Task.Run(() => DriveUsage.ListLargest(folder));
        }
        catch (Exception ex)
        {
            if (id != _scanId)
            {
                return;
            }

            ShowError(ex.Message);
            ScanRing.IsActive = false;
            ScanStatus.Text = "Could not scan this folder.";
            _busy = false;
            return;
        }

        if (id != _scanId)
        {
            return;
        }

        _folderTotal = 0;
        foreach (var item in items)
        {
            _folderTotal += item.Size;
        }

        RefreshDriveCard();
        ItemsHost.Children.Clear();
        if (items.Count == 0)
        {
            ItemsHost.Children.Add(new TextBlock
            {
                Padding = new Thickness(20, 16, 20, 16),
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Text = DriveUsage.ReadDrive().Ready ? "Nothing here, or this folder could not be read." : "I: is not available."
            });
        }
        else
        {
            for (var i = 0; i < items.Count; i++)
            {
                ItemsHost.Children.Add(CreateRow(items[i], i < items.Count - 1));
            }
        }

        ScanRing.IsActive = false;
        ScanStatus.Text = items.Count == 0
            ? "No items to show."
            : items.Count + " largest items in this folder";
        ClearError();
        _busy = false;
    }

    private void RefreshDriveCard()
    {
        var drive = DriveUsage.ReadDrive();
        if (!drive.Ready)
        {
            DriveTitle.Text = "I: is not available";
            DriveUsed.Text = "The persistent drive is missing on this machine.";
            DriveBar.Value = 0;
            DriveWarning.Visibility = Visibility.Visible;
            return;
        }

        DriveTitle.Text = DriveUsage.FormatBytes(drive.Total);
        DriveUsed.Text = DriveUsage.FormatBytes(drive.Used) + " used  ·  " +
                         DriveUsage.FormatBytes(drive.Free) + " free";
        DriveBar.Value = Math.Clamp(drive.UsedPercent, 0, 100);
        DriveWarning.Visibility = drive.Persistent ? Visibility.Collapsed : Visibility.Visible;
    }

    private FrameworkElement CreateRow(StorageEntry item, bool divider)
    {
        var share = _folderTotal > 0 ? item.Size / (double)_folderTotal : 0;
        var row = new Grid
        {
            MinHeight = 52,
            Padding = new Thickness(16, 10, 12, 10),
            ColumnSpacing = 12,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(new FontIcon
        {
            FontSize = 18,
            Glyph = item.IsDirectory ? "\uE8B7" : "\uE8A5",
            VerticalAlignment = VerticalAlignment.Center
        });

        var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        name.Children.Add(new TextBlock
        {
            FontSize = 14,
            Text = item.Name,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        name.Children.Add(new TextBlock
        {
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Text = item.IsDirectory ? "Folder" : "File"
        });
        Grid.SetColumn(name, 1);
        row.Children.Add(name);

        var bar = new ProgressBar
        {
            Height = 6,
            Maximum = 100,
            Value = Math.Clamp(share * 100, 0, 100),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(bar, 2);
        row.Children.Add(bar);

        var size = new TextBlock
        {
            FontSize = 12,
            Text = DriveUsage.FormatBytes(item.Size),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(size, 3);
        row.Children.Add(size);

        var delete = new Button
        {
            Content = "Delete",
            MinWidth = 0,
            Padding = new Thickness(10, 5, 10, 5),
            VerticalAlignment = VerticalAlignment.Center
        };
        delete.Click += (_, _) => _ = DeleteAsync(item);
        Grid.SetColumn(delete, 4);
        row.Children.Add(delete);

        row.PointerEntered += (_, _) =>
            row.Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        row.PointerExited += (_, _) =>
            row.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        row.Tapped += (_, args) =>
        {
            if (args.OriginalSource is DependencyObject source && IsInside(delete, source))
            {
                return;
            }

            OpenItem(item);
        };

        if (divider)
        {
            var wrap = new StackPanel();
            wrap.Children.Add(row);
            wrap.Children.Add(new Rectangle
            {
                Height = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Fill = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"]
            });
            return wrap;
        }

        return row;
    }

    private void OpenItem(StorageEntry item)
    {
        if (_busy)
        {
            return;
        }

        if (item.IsDirectory)
        {
            _ = LoadAsync(item.Path, recordHistory: true);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + item.Path + "\"",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private async Task DeleteAsync(StorageEntry item)
    {
        if (_busy)
        {
            return;
        }

        var extra = DriveUsage.IsSalsaNowFolder(item.Path)
            ? " This is inside I:\\Apps\\SalsaNOW."
            : string.Empty;
        var dialog = new ContentDialog
        {
            Title = item.IsDirectory ? "Delete folder?" : "Delete file?",
            Content = "Delete \"" + item.Name + "\"" +
                      (item.IsDirectory ? " and everything inside it?" : "?") +
                      extra + " This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (!DriveUsage.TryDelete(item.Path, out var error) &&
            !string.Equals(error, "That item is already gone.", StringComparison.Ordinal))
        {
            ShowError(error);
            return;
        }

        ClearError();
        ItemsHost.Children.Clear();
        ScanStatus.Text = "Refreshing...";
        _busy = false;
        await LoadAsync(_folder, recordHistory: false);
    }

    private static bool IsInside(FrameworkElement root, DependencyObject source)
    {
        var current = source;
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

    private void UpdateChrome()
    {
        BackButton.IsEnabled = _back.Count > 0;
        ForwardButton.IsEnabled = _forward.Count > 0;
        UpButton.IsEnabled = DriveUsage.ParentPath(_folder) is not null;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(left.TrimEnd('\\'), right.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

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
}
