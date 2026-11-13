using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media.Imaging;

namespace GmStudio;

internal sealed class SkillArtwork(string root)
{
    private const string Library = "RoleSkillInterface";
    private const string ExportPrefix = "skillicon_";
    private readonly Lazy<Dictionary<string, BitmapSource>> icons = new(() => Load(root));

    internal BitmapSource? Icon(string code) => icons.Value.GetValueOrDefault(code);

    private static Dictionary<string, BitmapSource> Load(string root)
    {
        try
        {
            foreach (string path in Directory.GetFiles(Path.Combine(root, "game"), Library + "*.swf")
                .OrderByDescending(path => int.TryParse(Path.GetFileNameWithoutExtension(path)[Library.Length..].TrimStart('v', 'V'),
                    out int version) ? version : 0))
            {
                try
                {
                    var icons = ReadIcons(path);
                    if (icons.Count > 0)
                        return icons;
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
        return new(StringComparer.Ordinal);
    }

    private static Dictionary<string, BitmapSource> ReadIcons(string path)
    {
        var tags = SwfTags.Read(path);
        var symbols = new Dictionary<int, string>();
        foreach (var (kind, data) in tags)
        {
            if (kind is 56 or 76)
                ReadSymbols(data.Span, symbols);
        }

        var icons = new Dictionary<string, BitmapSource>(StringComparer.Ordinal);
        foreach (var (kind, data) in tags)
        {
            // The local skill interface stores its exported icons as complete JPEGs.
            if (kind is not (6 or 21) || data.Length < 4 ||
                !symbols.TryGetValue(BinaryPrimitives.ReadUInt16LittleEndian(data.Span), out string? code))
                continue;
            if (data.Span[2] != 0xff || data.Span[3] != 0xd8)
                continue;
            try
            {
                using var stream = new MemoryStream(data[2..].ToArray());
                var image = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                image.Freeze();
                icons[code] = image;
            }
            catch (Exception ex) when (ex is IOException or FileFormatException or ArgumentException or NotSupportedException or COMException) { }
        }
        return icons;
    }

    private static void ReadSymbols(ReadOnlySpan<byte> data, Dictionary<int, string> symbols)
    {
        if (data.Length < 2)
            return;
        int count = BinaryPrimitives.ReadUInt16LittleEndian(data);
        data = data[2..];
        for (int i = 0; i < count && data.Length >= 3; i++)
        {
            int id = BinaryPrimitives.ReadUInt16LittleEndian(data);
            data = data[2..];
            int end = data.IndexOf((byte)0);
            if (end < 0)
                break;
            string name = Encoding.UTF8.GetString(data[..end]);
            if (name.StartsWith(ExportPrefix, StringComparison.Ordinal))
                symbols[id] = name[ExportPrefix.Length..];
            data = data[(end + 1)..];
        }
    }
}
