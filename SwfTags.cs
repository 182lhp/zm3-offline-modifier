using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace GmStudio;

internal static class SwfTags
{
    internal static IReadOnlyList<(int Kind, ReadOnlyMemory<byte> Data)> Read(string path)
    {
        using var source = File.OpenRead(path);
        Span<byte> header = stackalloc byte[8];
        source.ReadExactly(header);
        int size = BinaryPrimitives.ReadInt32LittleEndian(header[4..]) - header.Length;
        if (size is <= 0 or > 16 * 1024 * 1024)
            return [];
        byte[] body = new byte[size];
        if (header[..3].SequenceEqual("CWS"u8))
        {
            using var stream = new ZLibStream(source, CompressionMode.Decompress);
            stream.ReadExactly(body);
        }
        else if (header[..3].SequenceEqual("FWS"u8))
            source.ReadExactly(body);
        else
            return [];

        var tags = new List<(int, ReadOnlyMemory<byte>)>();
        int offset = (5 + 4 * (body[0] >> 3) + 7) / 8 + 4;
        while (offset <= body.Length - 2)
        {
            int tag = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(offset));
            offset += 2;
            int length = tag & 63;
            if (length == 63)
            {
                if (offset > body.Length - 4)
                    return [];
                length = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(offset));
                offset += 4;
            }
            if (length < 0 || length > body.Length - offset)
                return [];
            int kind = tag >> 6;
            if (kind == 0)
                break;
            tags.Add((kind, body.AsMemory(offset, length)));
            offset += length;
        }
        return tags;
    }
}
