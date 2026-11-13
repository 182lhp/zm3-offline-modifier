using System.Reflection;
using System.Windows.Media.Imaging;

namespace GmStudio;

internal static class PetArtwork
{
    private static readonly Dictionary<int, BitmapImage> nian = new();

    // The local icon table incorrectly maps every nian save code to nian5.png.
    // PetNian2 and PetNian3 inherit PetNian1's Monster1551 artwork. PetNian4
    // uses Monster155, and PetNian5 uses PetNianBmd5 (nianv1040.swf).
    internal static BitmapImage? Nian(string code)
    {
        int form = code switch { "nian1" or "nian2" or "nian3" => 1, "nian4" => 4, "nian5" => 5, _ => 0 };
        if (form == 0) return null;
        if (nian.TryGetValue(form, out var cached)) return cached;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"GmStudio.Pets.nian{form}.png");
        if (stream is null) return null;
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = stream;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        nian.Add(form, image);
        return image;
    }
}
