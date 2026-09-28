using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SalsaNOWSettings.Helpers;
using SalsaNOWSettings.Models;
using Windows.System;

namespace SalsaNOWSettings.Controls;

public sealed partial class FileExplorerPickerDialog : UserControl
{
    private const string KindFolder = "folder";
    private const string KindThisPc = "thispc";
    private const string KindHome = "home";

    private readonly ObservableCollection<FileExplorerPlace> _places = [];
    private readonly ObservableCollection<FileExplorerItem> _items = [];
    private readonly List<Location> _back = [];
    private readonly List<Location> _forward = [];
    private readonly List<FileExplorerItem> _currentItems = [];

    private FileExplorerPickerOptions _options = new();
    private Location _current = Location.ThisPc();
    private FileExplorerItem? _clicked;
    private int _iconVersion;
    private int _listVersion;

    public FileExplorerPickerDialog()
    {
        InitializeComponent();
        PlacesList.ItemsSource = _places;
        FilesView.ItemsSource = _items;
    }

    public IReadOnlyList<string> SelectedPaths { get; private set; } = [];

    public event Action? Confirmed;

    public event Action? Cancelled;

    public void Configure(FileExplorerPickerOptions options)
    {
        _options = options;
        FilesView.SelectionMode = ListViewSelectionMode.None;
        NameLabel.Text = options.FoldersOnly ? "Folder name:" : "File name:";
        NameBox.IsReadOnly = options.FoldersOnly;
        TypeCombo.Items.Clear();
        TypeCombo.Items.Add(options.FoldersOnly
            ? "Folder"
            : FormatFilterLabel(options.FileExtensions));
        TypeCombo.SelectedIndex = 0;
        ConfirmButton.Content = options.ConfirmText;
        BuildPlaces();
        Navigate(ResolveStartLocation(options), recordHistory: false);
    }

    public bool TryCommit()
    {
        if (!TryCollectSelection(out var paths) || paths.Count == 0)
        {
            StatusText.Text = _options.FoldersOnly
                ? "Open a folder, or select one in the list."
                : "Select a file to open.";
            return false;
        }

        SelectedPaths = paths;
        return true;
    }

    private void BuildPlaces()
    {
        _places.Clear();
        AddPlace("Home", KindHome, null, "\uE80F", 0);
        AddPlace("Desktop", KindFolder, KnownExplorerFolders.Desktop, "\uE8FC", 0);
        AddPlace("Downloads", KindFolder, KnownExplorerFolders.Downloads, "\uE896", 0);
        AddPlace("Documents", KindFolder, KnownExplorerFolders.Documents, "\uE8A5", 0);
        AddPlace("Pictures", KindFolder, KnownExplorerFolders.Pictures, "\uE91B", 0);
        AddPlace("Music", KindFolder, KnownExplorerFolders.Music, "\uE8D6", 0);
        AddPlace("Videos", KindFolder, KnownExplorerFolders.Videos, "\uE714", 0);
        AddPlace("This PC", KindThisPc, null, "\uE7F4", 0);

        foreach (var drive in EnumerateDrives())
        {
            _places.Add(new FileExplorerPlace
            {
                Title = drive.Name,
                Kind = KindFolder,
                Path = drive.Path,
                Glyph = "\uEDA2",
                Indent = 16
            });
        }
    }

    private void AddPlace(string title, string kind, string? path, string glyph, double indent)
    {
        if (kind == KindFolder && (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)))
        {
            return;
        }

        _places.Add(new FileExplorerPlace
        {
            Title = title,
            Kind = kind,
            Path = path,
            Glyph = glyph,
            Indent = indent
        });
    }

    private static Location ResolveStartLocation(FileExplorerPickerOptions options)
    {
        if (options.StartAtThisPc)
        {
            return Location.ThisPc();
        }

        if (!string.IsNullOrWhiteSpace(options.InitialPath))
        {
            try
            {
                var full = Path.GetFullPath(options.InitialPath);
                if (Directory.Exists(full))
                {
                    return Location.Folder(full);
                }
            }
            catch
            {
            }
        }

        var desktop = KnownExplorerFolders.Desktop;
        return Directory.Exists(desktop) ? Location.Folder(desktop) : Location.ThisPc();
    }

    private void Navigate(Location location, bool recordHistory)
    {
        if (recordHistory && !_current.Equals(location))
        {
            _back.Add(_current);
            _forward.Clear();
        }

        _current = location;
        SearchBox.Text = string.Empty;
        ReloadCurrent();
        UpdateChrome();
        SyncPlaceSelection();
    }

    private void ReloadCurrent()
    {
        var version = ++_listVersion;
        _iconVersion++;
        _clicked = null;
        _currentItems.Clear();
        _items.Clear();
        StatusText.Text = "Loading...";
        NewFolderButton.IsEnabled = _current.Kind == KindFolder && Directory.Exists(_current.Path);
        UpdateNameFromSelection();

        var current = _current;
        var foldersOnly = _options.FoldersOnly;
        var extensions = _options.FileExtensions;
        _ = Task.Run(() =>
        {
            List<FileExplorerItem> listed;
            try
            {
                listed = current.Kind == KindThisPc
                    ? EnumerateDrives().ToList()
                    : current.Kind == KindHome
                        ? BuildHomeItems().ToList()
                        : EnumerateFolder(current.Path, foldersOnly, extensions).ToList();
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (version == _listVersion)
                    {
                        StatusText.Text = ex.Message;
                    }
                });
                return;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                if (version != _listVersion)
                {
                    return;
                }

                _currentItems.Clear();
                _currentItems.AddRange(listed);
                ApplyFilter();
                UpdateNameFromSelection();
                StatusText.Text = string.Empty;
                QueueIcons();
            });
        });
    }

    private void QueueIcons()
    {
        var version = ++_iconVersion;
        var items = _items.Where(item => item.Icon is null).ToList();
        _ = Task.Run(() =>
        {
            foreach (var item in items)
            {
                if (version != _iconVersion)
                {
                    return;
                }

                var size = !item.IsDirectory && ShellIcon.IsImage(item.Path) ? 96 : 48;
                var bits = ShellIcon.GetBits(item.Path, item.IsDirectory, item.IsDrive, size);
                if (bits is null)
                {
                    continue;
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (version == _iconVersion)
                    {
                        item.Icon = ShellIcon.ToImage(bits.Value);
                    }
                });
            }
        });
    }

    private static IEnumerable<FileExplorerItem> BuildHomeItems()
    {
        if (PlaceItem("Desktop", KnownExplorerFolders.Desktop, "\uE8FC") is { } desktop)
        {
            yield return desktop;
        }

        if (PlaceItem("Downloads", KnownExplorerFolders.Downloads, "\uE896") is { } downloads)
        {
            yield return downloads;
        }

        if (PlaceItem("Documents", KnownExplorerFolders.Documents, "\uE8A5") is { } documents)
        {
            yield return documents;
        }

        if (PlaceItem("Pictures", KnownExplorerFolders.Pictures, "\uE91B") is { } pictures)
        {
            yield return pictures;
        }

        if (PlaceItem("Music", KnownExplorerFolders.Music, "\uE8D6") is { } music)
        {
            yield return music;
        }

        if (PlaceItem("Videos", KnownExplorerFolders.Videos, "\uE714") is { } videos)
        {
            yield return videos;
        }

        foreach (var drive in EnumerateDrives())
        {
            yield return drive;
        }
    }

    private static FileExplorerItem? PlaceItem(string name, string path, string glyph) =>
        string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)
            ? null
            : new FileExplorerItem
            {
                Name = name,
                Path = path,
                IsDirectory = true,
                Glyph = glyph
            };

    private static IEnumerable<FileExplorerItem> EnumerateDrives()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            var root = drive.Name;
            var title = root.TrimEnd('\\');
            try
            {
                title = drive.DriveType == DriveType.Fixed
                    ? $"Local Disk ({root.TrimEnd('\\')})"
                    : $"{drive.DriveType} ({root.TrimEnd('\\')})";
            }
            catch
            {
                title = root.TrimEnd('\\');
            }

            yield return new FileExplorerItem
            {
                Name = title,
                Path = root,
                IsDirectory = true,
                IsDrive = true,
                Glyph = "\uEDA2"
            };
        }
    }

    private static IEnumerable<FileExplorerItem> EnumerateFolder(
        string path,
        bool foldersOnly,
        IReadOnlyList<string> extensions)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
        };

        var items = new List<(string Path, bool IsDirectory, DateTime Written)>();
        foreach (var folder in Directory.EnumerateDirectories(path, "*", options))
        {
            items.Add((folder, true, GetLastWriteUtc(folder)));
        }

        if (!foldersOnly)
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", options))
            {
                if (MatchesFilter(file, extensions))
                {
                    items.Add((file, false, GetLastWriteUtc(file)));
                }
            }
        }

        return items
            .OrderByDescending(item => item.Written)
            .ThenBy(item => Path.GetFileName(item.Path), StringComparer.OrdinalIgnoreCase)
            .Take(1500)
            .Select(item => new FileExplorerItem
            {
                Name = Path.GetFileName(item.Path),
                Path = item.Path,
                IsDirectory = item.IsDirectory,
                Glyph = item.IsDirectory ? "\uE8B7" : "\uE7C3"
            });
    }

    private static DateTime GetLastWriteUtc(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private bool MatchesFilter(string filePath) =>
        MatchesFilter(filePath, _options.FileExtensions);

    private static bool MatchesFilter(string filePath, IReadOnlyList<string> extensions)
    {
        if (extensions.Count == 0)
        {
            return true;
        }

        var ext = Path.GetExtension(filePath);
        return extensions.Any(filter =>
            filter == "*" ||
            filter == ".*" ||
            ext.Equals(filter, StringComparison.OrdinalIgnoreCase) ||
            ext.Equals("." + filter.TrimStart('.'), StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        _items.Clear();
        foreach (var item in _currentItems)
        {
            if (query.Length == 0 ||
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                _items.Add(item);
            }
        }

        StatusText.Text = _items.Count == 0 && _currentItems.Count > 0
            ? "No matching items."
            : string.Empty;
    }

    private void UpdateChrome()
    {
        AddressBox.Text = _current.Kind switch
        {
            KindThisPc => "This PC",
            KindHome => "Home",
            _ => _current.Path
        };
        SearchBox.PlaceholderText = _current.Kind switch
        {
            KindThisPc => "Search This PC",
            KindHome => "Search Home",
            _ => $"Search {Path.GetFileName(_current.Path.TrimEnd('\\'))}"
        };
        BackButton.IsEnabled = _back.Count > 0;
        ForwardButton.IsEnabled = _forward.Count > 0;
        UpButton.IsEnabled = _current.Kind == KindFolder;
        NewFolderButton.IsEnabled = _current.Kind == KindFolder && Directory.Exists(_current.Path);
    }

    private void SyncPlaceSelection()
    {
        FileExplorerPlace? match;
        if (_current.Kind == KindThisPc)
        {
            match = _places.FirstOrDefault(place => place.Kind == KindThisPc);
        }
        else if (_current.Kind == KindHome)
        {
            match = _places.FirstOrDefault(place => place.Kind == KindHome);
        }
        else
        {
            match = _places
                .Where(place => place.Kind == KindFolder && !string.IsNullOrWhiteSpace(place.Path))
                .OrderByDescending(place => place.Path!.Length)
                .FirstOrDefault(place =>
                    PathsEqual(_current.Path, place.Path!) ||
                    _current.Path.StartsWith(AppendSlash(place.Path!), StringComparison.OrdinalIgnoreCase));
        }

        foreach (var place in _places)
        {
            place.IsCurrent = ReferenceEquals(place, match);
        }
    }

    private void UpdateNameFromSelection()
    {
        if (_clicked is not null)
        {
            NameBox.Text = _clicked.Name;
            return;
        }

        if (_current.Kind == KindFolder)
        {
            NameBox.Text = Path.GetFileName(_current.Path.TrimEnd('\\'));
            return;
        }

        NameBox.Text = _current.Kind == KindThisPc ? "This PC" : "Home";
    }

    private bool TryCollectSelection(out IReadOnlyList<string> paths)
    {
        var selected = _clicked is null ? [] : new List<FileExplorerItem> { _clicked };
        if (_options.FoldersOnly)
        {
            var folders = selected
                .Where(item => item.IsDirectory && Directory.Exists(item.Path))
                .Select(item => Path.GetFullPath(item.Path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (folders.Count > 0)
            {
                paths = folders;
                return true;
            }

            if (_current.Kind == KindFolder && Directory.Exists(_current.Path))
            {
                paths = [Path.GetFullPath(_current.Path)];
                return true;
            }

            paths = [];
            return false;
        }

        var files = selected
            .Where(item => !item.IsDirectory && File.Exists(item.Path) && MatchesFilter(item.Path))
            .Select(item => Path.GetFullPath(item.Path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0 && _current.Kind == KindFolder)
        {
            var typed = NameBox.Text?.Trim() ?? string.Empty;
            if (typed.Length > 0 &&
                !typed.EndsWith(" items", StringComparison.OrdinalIgnoreCase))
            {
                var candidate = Path.IsPathRooted(typed)
                    ? typed
                    : Path.Combine(_current.Path, typed);
                if (File.Exists(candidate) && MatchesFilter(candidate))
                {
                    files.Add(Path.GetFullPath(candidate));
                }
            }
        }

        paths = files;
        return files.Count > 0;
    }

    private void OpenItem(FileExplorerItem item)
    {
        if (item.IsDirectory)
        {
            if (!Directory.Exists(item.Path))
            {
                StatusText.Text = $"'{item.Name}' is not available.";
                return;
            }

            Navigate(Location.Folder(item.Path), recordHistory: true);
            return;
        }

        if (!_options.FoldersOnly && TryCommit())
        {
            Confirmed?.Invoke();
        }
    }

    private void PlacesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not FileExplorerPlace place)
        {
            return;
        }

        var target = place.Kind switch
        {
            KindThisPc => Location.ThisPc(),
            KindHome => Location.Home(),
            _ => Location.Folder(place.Path ?? string.Empty)
        };
        DispatcherQueue.TryEnqueue(() => Navigate(target, recordHistory: true));
    }

    private void FilesView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FileExplorerItem item)
        {
            _clicked = item;
            NameBox.Text = item.Name;
        }
    }

    private void FilesView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_clicked is not null)
        {
            OpenItem(_clicked);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) =>
        Cancelled?.Invoke();

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryCommit())
        {
            Confirmed?.Invoke();
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_back.Count == 0)
        {
            return;
        }

        _forward.Add(_current);
        var previous = _back[^1];
        _back.RemoveAt(_back.Count - 1);
        _current = previous;
        SearchBox.Text = string.Empty;
        ReloadCurrent();
        UpdateChrome();
        SyncPlaceSelection();
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (_forward.Count == 0)
        {
            return;
        }

        _back.Add(_current);
        var next = _forward[^1];
        _forward.RemoveAt(_forward.Count - 1);
        _current = next;
        SearchBox.Text = string.Empty;
        ReloadCurrent();
        UpdateChrome();
        SyncPlaceSelection();
    }

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        if (_current.Kind != KindFolder)
        {
            return;
        }

        try
        {
            var parent = Directory.GetParent(_current.Path);
            Navigate(parent is null ? Location.ThisPc() : Location.Folder(parent.FullName), recordHistory: true);
        }
        catch
        {
            Navigate(Location.ThisPc(), recordHistory: true);
        }
    }

    private void AddressBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        var text = AddressBox.Text?.Trim() ?? string.Empty;
        if (text.Equals("This PC", StringComparison.OrdinalIgnoreCase))
        {
            Navigate(Location.ThisPc(), recordHistory: true);
            return;
        }

        if (text.Equals("Home", StringComparison.OrdinalIgnoreCase))
        {
            Navigate(Location.Home(), recordHistory: true);
            return;
        }

        try
        {
            var full = Path.GetFullPath(text.Trim('"'));
            if (Directory.Exists(full))
            {
                Navigate(Location.Folder(full), recordHistory: true);
            }
            else
            {
                StatusText.Text = "That folder does not exist.";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (_current.Kind == KindThisPc)
        {
            BuildPlaces();
        }

        ReloadCurrent();
        UpdateChrome();
        SyncPlaceSelection();
    }

    private void NewFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_current.Kind != KindFolder || !Directory.Exists(_current.Path))
        {
            return;
        }

        var created = CreateUniqueFolder(_current.Path, "New folder");
        ReloadCurrent();
        var item = _items.FirstOrDefault(entry =>
            entry.Path.Equals(created, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            _clicked = item;
            NameBox.Text = item.Name;
        }
    }

    private static string CreateUniqueFolder(string parent, string baseName)
    {
        var path = Path.Combine(parent, baseName);
        var n = 2;
        while (Directory.Exists(path) || File.Exists(path))
        {
            path = Path.Combine(parent, $"{baseName} ({n})");
            n++;
        }

        Directory.CreateDirectory(path);
        return path;
    }

    private static string FormatFilterLabel(IReadOnlyList<string> extensions)
    {
        if (extensions.Count == 0)
        {
            return "All files (*.*)";
        }

        var joined = string.Join(";", extensions.Select(ext =>
            ext.StartsWith("*.", StringComparison.Ordinal) ? ext :
            ext.StartsWith('.') ? "*" + ext : "*." + ext));
        return $"Files ({joined})";
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);

    private static string AppendSlash(string path)
    {
        path = Path.TrimEndingDirectorySeparator(path);
        return path + Path.DirectorySeparatorChar;
    }

    private readonly record struct Location(string Kind, string Path)
    {
        public static Location ThisPc() => new(KindThisPc, string.Empty);
        public static Location Home() => new(KindHome, string.Empty);
        public static Location Folder(string path) => new(KindFolder, System.IO.Path.GetFullPath(path));
    }
}
