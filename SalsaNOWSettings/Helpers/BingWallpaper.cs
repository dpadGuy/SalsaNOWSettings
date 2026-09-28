using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SalsaNOWSettings.Helpers;

public static class BingWallpaper
{
    private const string ArchiveUrl = "https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt={0}";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(25) };

    public static bool IsEnabled()
    {
        try
        {
            SalsaNOWConfig.EnsureExists();
            return SalsaNOWConfig.Current.BingWallpaper == true;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        SalsaNOWConfig.Update(config => config.BingWallpaper = enabled ? true : null);
    }

    public static async Task<(bool Ok, string? Error)> ApplyTodayAsync()
    {
        try
        {
            var market = CultureInfo.CurrentUICulture.Name;
            if (string.IsNullOrWhiteSpace(market))
            {
                market = "en-US";
            }

            using var document = JsonDocument.Parse(await Client.GetStringAsync(string.Format(ArchiveUrl, market)));
            if (!document.RootElement.TryGetProperty("images", out var images) ||
                images.GetArrayLength() == 0 ||
                !images[0].TryGetProperty("urlbase", out var urlBaseProperty))
            {
                return (false, "Bing did not return today's photo.");
            }

            var urlBase = urlBaseProperty.GetString();
            if (string.IsNullOrWhiteSpace(urlBase))
            {
                return (false, "Bing did not return today's photo.");
            }

            var folder = Path.Combine(Path.GetTempPath(), "SalsaNOWSettings");
            Directory.CreateDirectory(folder);
            var temp = Path.Combine(folder, "bing-wallpaper.jpg");

            using (var response = await Client.GetAsync("https://www.bing.com" + urlBase + "_UHD.jpg"))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync();
                await using var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None);
                await input.CopyToAsync(output);
            }

            if (!File.Exists(temp) || !DesktopWallpaper.Apply(temp, bingWallpaper: true))
            {
                return (false, "Couldn't apply today's Bing wallpaper.");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
