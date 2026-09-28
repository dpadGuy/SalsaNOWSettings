using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SalsaNOWSettings.Controls;
using SalsaNOWSettings.Models;
using Windows.UI;

namespace SalsaNOWSettings.Helpers;

public static class FileExplorerPicker
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<string?> PickFileAsync(
        IReadOnlyList<string> extensions,
        string title = "Open",
        string? initialPath = null)
    {
        var paths = await ShowAsync(new FileExplorerPickerOptions
        {
            Title = title,
            ConfirmText = "Open",
            InitialPath = initialPath,
            FoldersOnly = false,
            AllowMultiSelect = false,
            FileExtensions = extensions
        });
        return paths.Count > 0 ? paths[0] : null;
    }

    public static async Task<string?> PickFolderAsync(
        string title = "Select folder",
        string? initialPath = null)
    {
        var paths = await ShowAsync(new FileExplorerPickerOptions
        {
            Title = title,
            ConfirmText = "Select Folder",
            InitialPath = initialPath,
            FoldersOnly = true,
            AllowMultiSelect = false
        });
        return paths.Count > 0 ? paths[0] : null;
    }

    private static async Task<IReadOnlyList<string>> ShowAsync(FileExplorerPickerOptions options)
    {
        var host = SettingsShell.Current?.OverlayHost ?? App.MainWindow?.Content as Panel;
        if (host is null)
        {
            return [];
        }

        await Gate.WaitAsync();
        Grid? overlay = null;
        try
        {
            var picker = new FileExplorerPickerDialog();
            picker.Configure(options);

            var finished = new TaskCompletionSource<IReadOnlyList<string>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            overlay = BuildOverlay(options.Title, picker, finished);
            if (host is Grid grid)
            {
                Grid.SetRow(overlay, 0);
                Grid.SetRowSpan(overlay, Math.Max(1, grid.RowDefinitions.Count));
                Grid.SetColumnSpan(overlay, Math.Max(1, grid.ColumnDefinitions.Count));
            }

            Canvas.SetZIndex(overlay, 1000);
            host.Children.Add(overlay);

            return await finished.Task;
        }
        catch
        {
            return [];
        }
        finally
        {
            if (overlay is not null)
            {
                host.Children.Remove(overlay);
            }

            Gate.Release();
        }
    }

    private static Grid BuildOverlay(
        string title,
        FileExplorerPickerDialog picker,
        TaskCompletionSource<IReadOnlyList<string>> finished)
    {
        void Complete(IReadOnlyList<string> paths) => finished.TrySetResult(paths);

        picker.Confirmed += () => Complete(picker.SelectedPaths);
        picker.Cancelled += () => Complete([]);
        picker.HorizontalAlignment = HorizontalAlignment.Stretch;
        picker.VerticalAlignment = VerticalAlignment.Stretch;
        picker.Margin = new Thickness(16, 0, 16, 16);

        var header = new TextBlock
        {
            Margin = new Thickness(16, 12, 16, 8),
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = title
        };

        var overlay = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = PanelBrush()
        };
        overlay.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        overlay.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        overlay.Children.Add(header);
        Grid.SetRow(picker, 1);
        overlay.Children.Add(picker);
        return overlay;
    }

    private static SolidColorBrush PanelBrush()
    {
        var color = App.MainWindow?.Content is FrameworkElement root &&
                    root.ActualTheme == ElementTheme.Light
            ? Color.FromArgb(255, 243, 243, 243)
            : Color.FromArgb(255, 32, 32, 32);
        return new SolidColorBrush(color);
    }
}
