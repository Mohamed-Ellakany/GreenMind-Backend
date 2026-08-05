using System.Buffers.Binary;

namespace Greenmind.Infrastructure.Disease;

internal static class ImageFileSignature
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static (string ContentType, string Extension)? Detect(byte[] content)
    {
        var data = content.AsSpan();
        if (IsValidPng(data)) return ("image/png", ".png");
        if (IsValidJpeg(data)) return ("image/jpeg", ".jpg");
        return null;
    }

    private static bool IsValidPng(ReadOnlySpan<byte> data)
    {
        if (data.Length < 45 || !data[..8].SequenceEqual(PngSignature)) return false;
        var offset = 8;
        var sawHeader = false;
        var sawData = false;
        while (offset <= data.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            if (length > int.MaxValue) return false;
            var chunkLength = (int)length;
            var chunkEnd = (long)offset + 12 + chunkLength;
            if (chunkEnd > data.Length) return false;

            var type = data.Slice(offset + 4, 4);
            var payload = data.Slice(offset + 8, chunkLength);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 8 + chunkLength, 4));
            if (ComputeCrc32(type, payload) != expectedCrc) return false;

            if (type.SequenceEqual("IHDR"u8))
            {
                if (sawHeader || offset != 8 || chunkLength != 13 ||
                    BinaryPrimitives.ReadUInt32BigEndian(payload[..4]) == 0 ||
                    BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4)) == 0 ||
                    payload[10] != 0 || payload[11] != 0 || payload[12] > 1)
                    return false;
                sawHeader = true;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (!sawHeader) return false;
                sawData = true;
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                return sawHeader && sawData && chunkLength == 0 && chunkEnd == data.Length;
            }

            offset = (int)chunkEnd;
        }
        return false;
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in first) crc = UpdateCrc(crc, value);
        foreach (var value in second) crc = UpdateCrc(crc, value);
        return ~crc;
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        return crc;
    }

    private static bool IsValidJpeg(ReadOnlySpan<byte> data)
    {
        if (data.Length < 20 || data[0] != 0xFF || data[1] != 0xD8 ||
            data[^2] != 0xFF || data[^1] != 0xD9)
            return false;

        var offset = 2;
        var sawFrame = false;
        while (offset < data.Length - 2)
        {
            if (data[offset++] != 0xFF) return false;
            while (offset < data.Length && data[offset] == 0xFF) offset++;
            if (offset >= data.Length) return false;
            var marker = data[offset++];
            if (marker == 0xD9) return false;
            if (marker is 0x01 or >= 0xD0 and <= 0xD7) continue;
            if (offset > data.Length - 2) return false;
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
            if (segmentLength < 2 || offset + segmentLength > data.Length) return false;
            if (IsStartOfFrame(marker)) sawFrame = true;
            if (marker == 0xDA) return sawFrame;
            offset += segmentLength;
        }
        return false;
    }

    private static bool IsStartOfFrame(byte marker) =>
        marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or
            >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF;
}
