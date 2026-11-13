using System.IO;
using System.Windows.Media.Imaging;

namespace GmStudio;

internal static class UtilityArtwork
{
    private static readonly Dictionary<string, BitmapImage?> cache = [];
    internal static BitmapImage? Image(string name)
    {
        if (cache.TryGetValue(name, out var image)) return image;
        using var stream = typeof(UtilityArtwork).Assembly.GetManifestResourceStream("GmStudio.Utility." + name);
        if (stream is not null)
        {
            image = new BitmapImage(); image.BeginInit(); image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad; image.EndInit(); image.Freeze();
        }
        cache[name] = image;
        return image;
    }
}
