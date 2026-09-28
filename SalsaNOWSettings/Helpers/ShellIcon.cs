using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;

namespace SalsaNOWSettings.Helpers;

internal static class ShellIcon
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallIcon = 0x000000001;
    private const uint ShgfiUseFileAttributes = 0x000000010;
    private const uint ShgfiIconLocation = 0x000001000;
    private const uint ShgfiSysIconIndex = 0x000004000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeNormal = 0x00000080;
    private const int ShilExtraLarge = 2;
    private const int ShilLarge = 0;
    private const int ShilSmall = 1;
    private const int IldImage = 0x00000020;
    private const int IldTransparent = 0x00000001;

    private static readonly Guid ImageListIid = new("46EB5926-582E-4017-9FDF-E8998DAA0950");
    private static readonly object CacheLock = new();
    private static readonly Dictionary<string, IconBits> BitCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ImageSource> ImageCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp",
        ".gif",
        ".jfif",
        ".jpe",
        ".jpeg",
        ".jpg",
        ".jxr",
        ".png",
        ".tif",
        ".tiff",
        ".webp"
    };

    public static bool IsImage(string? path) =>
        !string.IsNullOrWhiteSpace(path) && ImageExtensions.Contains(Path.GetExtension(path));

    public readonly record struct IconBits(byte[] Pixels, int Size, string Key);

    public static IconBits? GetBits(string? path, bool directory, bool drive = false, int size = 32, bool small = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var pixels = PixelSize(size);
        var key = CacheKey(path, directory, drive, pixels, small);
        lock (CacheLock)
        {
            if (BitCache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        try
        {
            var bits = !directory && !drive && IsImage(path)
                ? ReadImagePreview(path, pixels, key) ?? ExtractBits(path, directory, drive, pixels, small, key)
                : ExtractBits(path, directory, drive, pixels, small, key);
            if (bits is not null)
            {
                lock (CacheLock)
                {
                    BitCache[key] = bits.Value;
                }
            }

            return bits;
        }
        catch
        {
            return null;
        }
    }

    public static ImageSource? ToImage(IconBits bits)
    {
        lock (CacheLock)
        {
            if (ImageCache.TryGetValue(bits.Key, out var cached))
            {
                return cached;
            }
        }

        var image = ToBitmap(bits.Pixels, bits.Size);
        lock (CacheLock)
        {
            ImageCache[bits.Key] = image;
        }

        return image;
    }

    private static string CacheKey(string path, bool directory, bool drive, int size, bool small)
    {
        var kind = small ? "s" : "l";
        if (directory && !drive)
        {
            return $"{kind}|{size}|*folder";
        }

        if (drive || NeedsUniqueIcon(path))
        {
            return $"{kind}|{size}|{path}";
        }

        return $"{kind}|{size}|*{Path.GetExtension(path)}";
    }

    private static bool NeedsUniqueIcon(string path)
    {
        var ext = Path.GetExtension(path);
        return IsImage(path) ||
               ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".msc", StringComparison.OrdinalIgnoreCase);
    }

    private static IconBits? ReadImagePreview(string path, int pixelSize, string key)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var stream = file.AsRandomAccessStream();
            var decoder = BitmapDecoder.CreateAsync(stream).AsTask().GetAwaiter().GetResult();
            var width = decoder.PixelWidth;
            var height = decoder.PixelHeight;
            if (width == 0 || height == 0)
            {
                return null;
            }

            var scale = pixelSize / (double)Math.Max(width, height);
            var scaledWidth = Math.Max(1u, (uint)Math.Round(width * scale));
            var scaledHeight = Math.Max(1u, (uint)Math.Round(height * scale));
            var data = decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform
                {
                    ScaledWidth = scaledWidth,
                    ScaledHeight = scaledHeight,
                    InterpolationMode = BitmapInterpolationMode.Fant
                },
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult();

            return new IconBits(
                ScaleTo(data.DetachPixelData(), (int)scaledWidth, (int)scaledHeight, pixelSize),
                pixelSize,
                key);
        }
        catch
        {
            return null;
        }
    }

    private static int PixelSize(int logical)
    {
        var scale = 1.0;
        try
        {
            if (App.MainWindow?.Content is FrameworkElement root && root.XamlRoot is not null)
            {
                scale = root.XamlRoot.RasterizationScale;
            }
        }
        catch
        {
        }

        if (scale < 1)
        {
            scale = 1;
        }

        return Math.Clamp((int)Math.Round(logical * scale), 16, 256);
    }

    private static IconBits? ExtractBits(string path, bool directory, bool drive, int pixelSize, bool small, string key)
    {
        var attributes = directory || drive ? FileAttributeDirectory : FileAttributeNormal;
        var query = path;
        var extra = 0u;

        if (!directory && !drive && !NeedsUniqueIcon(path))
        {
            extra = ShgfiUseFileAttributes;
            query = "file" + Path.GetExtension(path);
        }
        else if ((directory || drive) && !Directory.Exists(path))
        {
            extra = ShgfiUseFileAttributes;
        }
        else if (!directory && !drive && !File.Exists(path))
        {
            extra = ShgfiUseFileAttributes;
        }

        var raster = BestRaster(query, attributes, extra, small);
        return raster is null ? null : new IconBits(Fit(raster.Value, pixelSize), pixelSize, key);
    }

    private static IconRaster? BestRaster(string query, uint attributes, uint extra, bool small)
    {
        var info = new ShFileInfo();
        SHGetFileInfo(query, attributes, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiSysIconIndex | extra);

        var lists = small
            ? new[] { ShilSmall }
            : new[] { ShilExtraLarge, ShilLarge };

        IconRaster? best = null;
        var bestContent = 0;
        foreach (var listId in lists)
        {
            var raster = RasterFromIcon(IconFromImageList(info.iIcon, listId));
            if (raster is null)
            {
                continue;
            }

            var content = Math.Max(raster.Value.ContentWidth, raster.Value.ContentHeight);
            if (content > bestContent)
            {
                best = raster;
                bestContent = content;
            }

            if (content >= raster.Value.Width * 2 / 5)
            {
                break;
            }
        }

        var location = new ShFileInfo();
        SHGetFileInfo(query, attributes, ref location, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIconLocation | extra);
        if (!string.IsNullOrWhiteSpace(location.szDisplayName))
        {
            var fromFile = RasterFromIcon(IconFromFile(location.szDisplayName, location.iIcon, small ? 16 : 48));
            var content = fromFile is null ? 0 : Math.Max(fromFile.Value.ContentWidth, fromFile.Value.ContentHeight);
            if (fromFile is not null && content > bestContent)
            {
                best = fromFile;
                bestContent = content;
            }
        }

        if (best is not null)
        {
            return best;
        }

        var fallback = new ShFileInfo();
        var flags = ShgfiIcon | extra | (small ? ShgfiSmallIcon : 0u);
        var result = SHGetFileInfo(query, attributes, ref fallback, (uint)Marshal.SizeOf<ShFileInfo>(), flags);
        return result == nint.Zero ? null : RasterFromIcon(fallback.hIcon);
    }

    private static nint IconFromImageList(int index, int listId)
    {
        try
        {
            var iid = ImageListIid;
            if (SHGetImageList(listId, ref iid, out var list) != 0 || list is null)
            {
                return nint.Zero;
            }

            try
            {
                return list.GetIcon(index, IldImage | IldTransparent, out var icon) == 0
                    ? icon
                    : nint.Zero;
            }
            finally
            {
                Marshal.ReleaseComObject(list);
            }
        }
        catch
        {
            return nint.Zero;
        }
    }

    private static nint IconFromFile(string file, int iconIndex, int pixelSize)
    {
        if (SHDefExtractIcon(file, iconIndex, 0, out var large, nint.Zero, (uint)pixelSize) == 0 &&
            large != nint.Zero)
        {
            return large;
        }

        var icons = new nint[1];
        var ids = new int[1];
        if (PrivateExtractIcons(file, iconIndex, pixelSize, pixelSize, icons, ids, 1, 0) > 0)
        {
            return icons[0];
        }

        return nint.Zero;
    }

    private static IconRaster? RasterFromIcon(nint hIcon)
    {
        if (hIcon == nint.Zero)
        {
            return null;
        }

        try
        {
            if (!ReadIcon(hIcon, out var pixels, out var width, out var height) || pixels is null)
            {
                return null;
            }

            Measure(pixels, width, height, out var x, out var y, out var contentWidth, out var contentHeight);
            return new IconRaster(pixels, width, height, x, y, contentWidth, contentHeight);
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static void Measure(byte[] pixels, int width, int height, out int x, out int y, out int contentWidth, out int contentHeight)
    {
        var minX = width;
        var minY = height;
        var maxX = -1;
        var maxY = -1;
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                if (pixels[(row * width + column) * 4 + 3] < 16)
                {
                    continue;
                }

                minX = Math.Min(minX, column);
                minY = Math.Min(minY, row);
                maxX = Math.Max(maxX, column);
                maxY = Math.Max(maxY, row);
            }
        }

        if (maxX < minX)
        {
            x = 0;
            y = 0;
            contentWidth = width;
            contentHeight = height;
            return;
        }

        x = minX;
        y = minY;
        contentWidth = maxX - minX + 1;
        contentHeight = maxY - minY + 1;
    }

    private static byte[] Fit(IconRaster raster, int dest)
    {
        var cropped = Crop(raster);
        return ScaleTo(cropped.Pixels, cropped.Width, cropped.Height, dest);
    }

    private static (byte[] Pixels, int Width, int Height) Crop(IconRaster raster)
    {
        var pad = 1;
        var x = Math.Max(0, raster.X - pad);
        var y = Math.Max(0, raster.Y - pad);
        var width = Math.Min(raster.Width - x, raster.ContentWidth + pad * 2);
        var height = Math.Min(raster.Height - y, raster.ContentHeight + pad * 2);
        var pixels = new byte[width * height * 4];
        for (var row = 0; row < height; row++)
        {
            Buffer.BlockCopy(
                raster.Pixels,
                ((y + row) * raster.Width + x) * 4,
                pixels,
                row * width * 4,
                width * 4);
        }

        return (pixels, width, height);
    }

    private static byte[] ScaleTo(byte[] source, int sourceWidth, int sourceHeight, int dest)
    {
        var scale = dest / (double)Math.Max(sourceWidth, sourceHeight);
        var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        var sized = width == sourceWidth && height == sourceHeight
            ? source
            : Downscale(source, sourceWidth, sourceHeight, width, height);

        if (width == dest && height == dest)
        {
            return sized;
        }

        var canvas = new byte[dest * dest * 4];
        var left = (dest - width) / 2;
        var top = (dest - height) / 2;
        for (var row = 0; row < height; row++)
        {
            Buffer.BlockCopy(sized, row * width * 4, canvas, ((top + row) * dest + left) * 4, width * 4);
        }

        return canvas;
    }

    private static WriteableBitmap ToBitmap(byte[] pixels, int pixelSize)
    {
        var image = new WriteableBitmap(pixelSize, pixelSize);
        using (var stream = image.PixelBuffer.AsStream())
        {
            stream.Write(pixels, 0, pixels.Length);
        }

        image.Invalidate();
        return image;
    }

    private readonly record struct IconRaster(
        byte[] Pixels,
        int Width,
        int Height,
        int X,
        int Y,
        int ContentWidth,
        int ContentHeight);

    private static bool ReadIcon(nint hIcon, out byte[]? pixels, out int width, out int height)
    {
        pixels = null;
        width = 0;
        height = 0;
        if (!GetIconInfo(hIcon, out var info))
        {
            return false;
        }

        try
        {
            if (info.hbmColor == nint.Zero ||
                GetObject(info.hbmColor, Marshal.SizeOf<GdiBitmap>(), out var bitmap) == 0)
            {
                return false;
            }

            width = bitmap.bmWidth;
            height = Math.Abs(bitmap.bmHeight);
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            pixels = GetBgra(info.hbmColor, width, height);
            if (pixels is null)
            {
                return false;
            }

            if (!HasRealAlpha(pixels))
            {
                ApplyMask(pixels, info.hbmMask, width, height);
            }

            return true;
        }
        finally
        {
            if (info.hbmColor != nint.Zero)
            {
                DeleteObject(info.hbmColor);
            }

            if (info.hbmMask != nint.Zero)
            {
                DeleteObject(info.hbmMask);
            }
        }
    }

    private static byte[]? GetBgra(nint bitmap, int width, int height)
    {
        var header = new BitmapInfoHeader
        {
            biSize = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
            biWidth = width,
            biHeight = -height,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0
        };
        var pixels = new byte[width * height * 4];
        var hdc = GetDC(nint.Zero);
        try
        {
            return GetDIBits(hdc, bitmap, 0, (uint)height, pixels, ref header, 0) == 0
                ? null
                : pixels;
        }
        finally
        {
            ReleaseDC(nint.Zero, hdc);
        }
    }

    private static bool HasRealAlpha(byte[] pixels)
    {
        var sawClear = false;
        var sawOpaque = false;
        var sawPartial = false;
        for (var i = 3; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i];
            if (alpha == 0)
            {
                sawClear = true;
            }
            else if (alpha == 255)
            {
                sawOpaque = true;
            }
            else
            {
                sawPartial = true;
                break;
            }
        }

        return sawPartial || (sawClear && sawOpaque);
    }

    private static void ApplyMask(byte[] pixels, nint mask, int width, int height)
    {
        if (mask == nint.Zero)
        {
            return;
        }

        var bits = GetBgra(mask, width, height);
        if (bits is null)
        {
            return;
        }

        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 3] = bits[i] == 0 ? (byte)255 : (byte)0;
        }
    }

    private static byte[] Downscale(byte[] source, int sourceWidth, int sourceHeight, int destWidth, int destHeight)
    {
        var dest = new byte[destWidth * destHeight * 4];
        for (var y = 0; y < destHeight; y++)
        {
            var y0 = y * sourceHeight / destHeight;
            var y1 = Math.Max(y0 + 1, (y + 1) * sourceHeight / destHeight);
            for (var x = 0; x < destWidth; x++)
            {
                var x0 = x * sourceWidth / destWidth;
                var x1 = Math.Max(x0 + 1, (x + 1) * sourceWidth / destWidth);
                long blue = 0, green = 0, red = 0, alpha = 0, count = 0;
                for (var sy = y0; sy < y1; sy++)
                {
                    for (var sx = x0; sx < x1; sx++)
                    {
                        var i = (sy * sourceWidth + sx) * 4;
                        blue += source[i];
                        green += source[i + 1];
                        red += source[i + 2];
                        alpha += source[i + 3];
                        count++;
                    }
                }

                var o = (y * destWidth + x) * 4;
                dest[o] = (byte)(blue / count);
                dest[o + 1] = (byte)(green / count);
                dest[o + 2] = (byte)(red / count);
                dest[o + 3] = (byte)(alpha / count);
            }
        }

        return dest;
    }

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(nint hbmImage, nint hbmMask, out int pi);
        [PreserveSig] int ReplaceIcon(int i, nint hicon, out int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, nint hbmImage, nint hbmMask);
        [PreserveSig] int AddMasked(nint hbmImage, uint crMask, out int pi);
        [PreserveSig] int Draw(nint pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, out nint picon);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public nint hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public nint hbmMask;
        public nint hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GdiBitmap
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public nint bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHDefExtractIcon(
        string pszIconFile,
        int iIndex,
        uint uFlags,
        out nint phiconLarge,
        nint phiconSmall,
        uint nIconSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint PrivateExtractIcons(
        string szFileName,
        int nIconIndex,
        int cxIcon,
        int cyIcon,
        [Out] nint[] phicon,
        [Out] int[] piconid,
        uint nIcons,
        uint flags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref ShFileInfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(nint hIcon, out IconInfo piconinfo);

    [DllImport("user32.dll")]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint hWnd, nint hDc);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(nint hgdiobj, int cbBuffer, out GdiBitmap lpvObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        nint hdc,
        nint hbmp,
        uint uStartScan,
        uint cScanLines,
        byte[] lpvBits,
        ref BitmapInfoHeader lpbi,
        uint uUsage);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint ho);
}
