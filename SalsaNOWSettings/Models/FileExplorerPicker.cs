using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace SalsaNOWSettings.Models;

public sealed class FileExplorerPickerOptions
{
    public string Title { get; init; } = "Browse";
    public string ConfirmText { get; init; } = "Select Folder";
    public string? InitialPath { get; init; }
    public bool StartAtThisPc { get; init; }
    public bool AllowMultiSelect { get; init; }
    public bool FoldersOnly { get; init; } = true;
    public IReadOnlyList<string> FileExtensions { get; init; } = Array.Empty<string>();
}

public sealed class FileExplorerItem : INotifyPropertyChanged
{
    private ImageSource? _icon;

    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public bool IsDrive { get; init; }
    public string Glyph { get; init; } = "\uE8B7";

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (ReferenceEquals(_icon, value))
            {
                return;
            }

            _icon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GlyphVisibility)));
        }
    }

    public Visibility IconVisibility => _icon is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility GlyphVisibility => _icon is null ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class FileExplorerPlace : INotifyPropertyChanged
{
    private ImageSource? _icon;
    private bool _isCurrent;

    public string Title { get; init; } = string.Empty;
    public string? Path { get; init; }
    public string Kind { get; init; } = "folder";
    public string Glyph { get; init; } = "\uE8B7";
    public double Indent { get; init; }

    public Thickness IndentMargin => new(Indent, 0, 0, 0);

    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (ReferenceEquals(_icon, value))
            {
                return;
            }

            _icon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GlyphVisibility)));
        }
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent == value)
            {
                return;
            }

            _isCurrent = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCurrent)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RowBackground)));
        }
    }

    public Brush RowBackground =>
        _isCurrent
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(48, 255, 255, 255))
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    public Visibility IconVisibility => _icon is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility GlyphVisibility => _icon is null ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;
}
