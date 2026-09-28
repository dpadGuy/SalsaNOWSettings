using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SalsaNOWSettings.Helpers;

internal static class ShellThumbnail
{
    public static Task<BitmapImage?> GetAsync(string path, bool isDirectory, uint size = 64)
    {
        _ = path;
        _ = isDirectory;
        _ = size;
        return Task.FromResult<BitmapImage?>(null);
    }
}
