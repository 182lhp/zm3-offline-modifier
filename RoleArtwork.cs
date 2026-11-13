using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GmStudio;

internal sealed class RoleArtwork(string root)
{
    private const int FrameHeight = 200;
    private readonly Dictionary<string, BitmapSource?> frames = new(StringComparer.Ordinal);

    internal BitmapSource? Portrait(int role, int clothing, int weapon, out bool usedFallback)
    {
        usedFallback = false;
        if (role is < 1 or > 4)
            return null;
        int width = role == 3 ? 300 : 200;
        string body = role == 4 ? weapon is 4 or 5 or 7 or 8 or 9 ? "ROLE4_ARROW" : "ROLE4_SHOVEL" : $"ROLE{role}";
        var figure = Frame($"{body}_{clothing}", width);
        var held = Frame($"ROLE{role}_EQUIP_{weapon}", width);
        usedFallback = figure is null || held is null;
        figure ??= Frame($"{body}_0", width);
        if (figure is null)
            return null;
        held ??= Frame($"ROLE{role}_EQUIP_0", width);
        var drawing = new DrawingVisual();
        var frame = new Rect(0, 0, width, FrameHeight);
        using (DrawingContext context = drawing.RenderOpen())
        {
            context.DrawImage(figure, frame);
            if (held is not null)
                context.DrawImage(held, frame);
        }
        var image = new RenderTargetBitmap(width, FrameHeight, 96, 96, PixelFormats.Pbgra32);
        image.Render(drawing);
        return Trim(image);
    }

    private BitmapSource? Frame(string name, int width)
    {
        if (!frames.TryGetValue(name, out var image))
        {
            try { image = ReadFrame(Path.Combine(root, "game", name + ".swf"), width); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException
                or ArgumentException or FileFormatException or NotSupportedException or System.Runtime.InteropServices.COMException)
            { image = null; }
            frames.Add(name, image);
        }
        return image;
    }

    private static BitmapSource? ReadFrame(string path, int width)
    {
        foreach (var (kind, data) in SwfTags.Read(path))
        {
            if (kind == 35)
                return DecodeJpeg(data.Span, width);
            if (kind == 36)
                return DecodeLossless(data.Span, width);
        }
        return null;
    }

    // The inventory portrait uses the first 200px-tall cell of each body and weapon sheet.
    private static BitmapSource? DecodeJpeg(ReadOnlySpan<byte> tag, int width)
    {
        if (tag.Length < 6)
            return null;
        int jpegSize = BinaryPrimitives.ReadInt32LittleEndian(tag[2..]);
        if (jpegSize <= 0 || jpegSize >= tag.Length - 6)
            return null;
        using var jpeg = new MemoryStream(tag.Slice(6, jpegSize).ToArray());
        var decoded = BitmapFrame.Create(jpeg, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (!ValidSheet(decoded.PixelWidth, decoded.PixelHeight, width))
            return null;
        var converted = new FormatConvertedBitmap(decoded, PixelFormats.Bgra32, null, 0);
        byte[] pixels = new byte[width * FrameHeight * 4];
        converted.CopyPixels(new Int32Rect(0, 0, width, FrameHeight), pixels, width * 4, 0);
        byte[] alpha = ReadRows(tag[(6 + jpegSize)..], decoded.PixelWidth);
        for (int y = 0; y < FrameHeight; y++)
            for (int x = 0; x < width; x++)
                pixels[(y * width + x) * 4 + 3] = alpha[y * decoded.PixelWidth + x];
        return MakeBitmap(pixels, width, PixelFormats.Bgra32);
    }

    private static BitmapSource? DecodeLossless(ReadOnlySpan<byte> tag, int width)
    {
        if (tag.Length < 7 || tag[2] != 5)
            return null;
        int sheetWidth = BinaryPrimitives.ReadUInt16LittleEndian(tag[3..]);
        int sheetHeight = BinaryPrimitives.ReadUInt16LittleEndian(tag[5..]);
        if (!ValidSheet(sheetWidth, sheetHeight, width))
            return null;
        byte[] argb = ReadRows(tag[7..], sheetWidth * 4);
        byte[] pixels = new byte[width * FrameHeight * 4];
        for (int y = 0; y < FrameHeight; y++)
            for (int x = 0; x < width; x++)
            {
                int input = (y * sheetWidth + x) * 4, output = (y * width + x) * 4;
                pixels[output] = argb[input + 3];
                pixels[output + 1] = argb[input + 2];
                pixels[output + 2] = argb[input + 1];
                pixels[output + 3] = argb[input];
            }
        return MakeBitmap(pixels, width, PixelFormats.Pbgra32);
    }

    private static bool ValidSheet(int width, int height, int frameWidth) =>
        width >= frameWidth && height >= FrameHeight && (long)width * height <= 12_000_000;

    private static byte[] ReadRows(ReadOnlySpan<byte> compressed, int stride)
    {
        using var source = new MemoryStream(compressed.ToArray());
        using var stream = new ZLibStream(source, CompressionMode.Decompress);
        var rows = new byte[stride * FrameHeight];
        stream.ReadExactly(rows);
        return rows;
    }

    private static BitmapSource MakeBitmap(byte[] pixels, int width, PixelFormat format)
    {
        var image = BitmapSource.Create(width, FrameHeight, 96, 96, format, null, pixels, width * 4);
        image.Freeze();
        return image;
    }

    private static BitmapSource Trim(BitmapSource image)
    {
        int width = image.PixelWidth, height = image.PixelHeight;
        byte[] pixels = new byte[width * height * 4];
        image.CopyPixels(pixels, width * 4, 0);
        int left = width, top = height, right = -1, bottom = -1;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (pixels[(y * width + x) * 4 + 3] < 32)
                    continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        BitmapSource trimmed = right >= left && bottom >= top
            ? new CroppedBitmap(image, new Int32Rect(left, top, right - left + 1, bottom - top + 1))
            : image;
        trimmed.Freeze();
        return trimmed;
    }
}
