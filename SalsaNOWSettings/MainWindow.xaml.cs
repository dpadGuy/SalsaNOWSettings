using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SalsaNOWSettings.Controls;
using SalsaNOWSettings.Helpers;
using Windows.Graphics;
using WinRT.Interop;

namespace SalsaNOWSettings;

public sealed partial class MainWindow : Window
{
    private const int MinWidth = 1100;
    private const int MinHeight = 720;
    private const int MaxWidth = 1600;
    private const int MaxHeight = 1000;
    private const uint WmGetMinMaxInfo = 0x0024;
    private const uint WmSizing = 0x0214;
    private const nuint SubclassId = 0x534E5753;
    private const int WmszLeft = 1;
    private const int WmszTop = 3;
    private const int WmszTopLeft = 4;
    private const int WmszTopRight = 5;
    private const int WmszBottomLeft = 7;

    private readonly SubclassProc _subclassProc;

    public string VersionLabel => AppVersion.Display;

    public MainWindow()
    {
        InitializeComponent();
        _subclassProc = OnSubclass;

        try
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
        }
        catch (Exception)
        {
            ExtendsContentIntoTitleBar = false;
        }

        LimitResize();
        ApplyWindowIcon();
        WindowsColorSettings.ApplyAppTheme();
    }

    private void ApplyWindowIcon()
    {
        try
        {
            var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(icon))
            {
                AppWindow.SetIcon(icon);
            }
        }
        catch
        {
        }
    }

    public SettingsShell Settings => Shell;

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) =>
        Shell.HandleSearchTextChanged(sender, args);

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) =>
        Shell.HandleSearchQuerySubmitted(sender, args);

    private void LimitResize()
    {
        try
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = MinWidth;
                presenter.PreferredMinimumHeight = MinHeight;
            }
        }
        catch
        {
        }

        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            SetWindowSubclass(hwnd, _subclassProc, SubclassId, 0);
        }
        catch
        {
        }

        try
        {
            AppWindow.Resize(ToPixels(1400, 900));
        }
        catch
        {
        }
    }

    private nint OnSubclass(nint hwnd, uint message, nint wParam, nint lParam, nuint id, nuint data)
    {
        if (message == WmSizing && lParam != nint.Zero)
        {
            ClampDragSize(wParam.ToInt32(), lParam);
        }

        var result = DefSubclassProc(hwnd, message, wParam, lParam);
        if (message == WmGetMinMaxInfo && lParam != nint.Zero)
        {
            var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            var min = ToPixels(MinWidth, MinHeight);
            info.MinTrackSize = new Point { X = min.Width, Y = min.Height };
            Marshal.StructureToPtr(info, lParam, false);
        }

        return result;
    }

    private void ClampDragSize(int edge, nint rectPtr)
    {
        var rect = Marshal.PtrToStructure<Rect>(rectPtr);
        var min = ToPixels(MinWidth, MinHeight);
        var max = ToPixels(MaxWidth, MaxHeight);
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var leftEdge = edge is WmszLeft or WmszTopLeft or WmszBottomLeft;
        var topEdge = edge is WmszTop or WmszTopLeft or WmszTopRight;

        if (width > max.Width)
        {
            if (leftEdge)
            {
                rect.Left = rect.Right - max.Width;
            }
            else
            {
                rect.Right = rect.Left + max.Width;
            }
        }
        else if (width < min.Width)
        {
            if (leftEdge)
            {
                rect.Left = rect.Right - min.Width;
            }
            else
            {
                rect.Right = rect.Left + min.Width;
            }
        }

        if (height > max.Height)
        {
            if (topEdge)
            {
                rect.Top = rect.Bottom - max.Height;
            }
            else
            {
                rect.Bottom = rect.Top + max.Height;
            }
        }
        else if (height < min.Height)
        {
            if (topEdge)
            {
                rect.Top = rect.Bottom - min.Height;
            }
            else
            {
                rect.Bottom = rect.Top + min.Height;
            }
        }

        Marshal.StructureToPtr(rect, rectPtr, false);
    }

    private SizeInt32 ToPixels(int width, int height)
    {
        var scale = 1.0;
        try
        {
            if (Content is FrameworkElement root && root.XamlRoot is not null)
            {
                scale = root.XamlRoot.RasterizationScale;
            }
            else
            {
                scale = GetDpiForWindow(WindowNative.GetWindowHandle(this)) / 96.0;
            }
        }
        catch
        {
        }

        if (scale < 1)
        {
            scale = 1;
        }

        return new SizeInt32((int)Math.Round(width * scale), (int)Math.Round(height * scale));
    }

    private delegate nint SubclassProc(nint hwnd, uint message, nint wParam, nint lParam, nuint id, nuint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(nint hwnd, SubclassProc proc, nuint id, nuint data);

    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hwnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
