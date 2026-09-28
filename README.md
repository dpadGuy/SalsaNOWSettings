# SalsaNOW Settings

A Windows 11 Settings-style app for SalsaNOW GeForce NOW hosts. It writes everything to one JSON file that SalsaNOW reads on session start.

Windows Settings (`ms-settings:`) is not available in this environment, so this app is the place to change wallpaper, colors, the taskbar, default apps, and startup behavior.

## Build requirements

- Windows 10/11
- Visual Studio 2022+ with the WinUI / Windows App SDK workload
- .NET 8 and Windows App SDK 2.4
- Unpackaged project (`WindowsPackageType=None`) — open `SalsaNOWSettings.sln` and press F5

Target framework: `net8.0-windows10.0.19041.0`

## Config

All options are stored at:

```
I:\Apps\SalsaNOW\SalsaNOWSettings.json
```

If that file is missing, SalsaNOW skips the settings apply step.

A chosen wallpaper is also copied to `I:\Apps\SalsaNOW\DesktopWallpaper` as `wallpaper` plus the real extension, so it survives the next session.

## What you can change

**Home** — device info and shortcuts into the pages you actually use.

**Background** — inbox wallpapers, browse for a photo, or Bing wallpaper of the day. Turning Bing on downloads today’s photo and applies it immediately.

**Colors** — light / dark / custom, transparency, and accent color.

**Taskbar** — alignment, auto-hide, badges, flashing, share windows, show-desktop corner, combine buttons, smaller buttons, and full transparency via [TranslucentTB](https://github.com/TranslucentTB/TranslucentTB) (`I:\Apps\SalsaNOW\Zxplorer\TranslucentTB\TranslucentTB.exe`).

**Default apps** — pick an `.exe` for common file types (archives, text, photos, video, music) and a default browser. Associations are applied with SetUserFTA.

**Startup** — Steam silent launch, plus an optional `StartupBatch.bat` that SalsaNOW runs when a session begins.

**Storage / About** — `I:` usage, largest items, and machine details.

## SalsaNOW

SalsaNOW reads the same JSON before Steam and the desktop come up:

- `steamSilentLaunch` starts Steam with `-silent`
- `bingWallpaper` fetches today’s Bing photo
- colors, taskbar, TranslucentTB, and file associations are applied from the file
- if no custom wallpaper is staged, SalsaNOW downloads and applies the default wallpaper
