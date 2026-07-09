namespace eBookEditor.DocxImport.Services;

/// <summary>
/// Reads pixel width/height straight from PNG/JPEG file headers — enough to size an embedded
/// Word image via EMUs, without pulling in a full imaging library for this narrow need (QuestPDF's
/// PDF path doesn't need this since its .Image().FitWidth() auto-scales via its own decoding).
/// </summary>
internal static class ImageDimensionReader
{
    public static (int Width, int Height)? TryGetDimensions(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            var header = reader.ReadBytes(8);

            if (IsPng(header))
                return ReadPngDimensions(reader);

            if (header.Length >= 2 && header[0] == 0xFF && header[1] == 0xD8)
            {
                stream.Position = 2;
                return ReadJpegDimensions(reader);
            }
        }
        catch (IOException)
        {
        }

        return null;
    }

    private static bool IsPng(byte[] header) =>
        header.Length == 8 &&
        header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
        header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

    private static (int Width, int Height)? ReadPngDimensions(BinaryReader reader)
    {
        reader.ReadBytes(4); // IHDR chunk length
        reader.ReadBytes(4); // "IHDR"
        var width = ReadBigEndianInt32(reader);
        var height = ReadBigEndianInt32(reader);
        return (width, height);
    }

    private static (int Width, int Height)? ReadJpegDimensions(BinaryReader reader)
    {
        while (reader.BaseStream.Position < reader.BaseStream.Length - 1)
        {
            if (reader.ReadByte() != 0xFF)
                continue;

            var marker = reader.ReadByte();
            while (marker == 0xFF)
                marker = reader.ReadByte();

            if (marker is 0xD8 or 0xD9 or 0x01 || marker is >= 0xD0 and <= 0xD7)
                continue;

            var length = ReadBigEndianUInt16(reader);
            var isStartOfFrame = marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;

            if (isStartOfFrame)
            {
                reader.ReadByte(); // precision
                var height = ReadBigEndianUInt16(reader);
                var width = ReadBigEndianUInt16(reader);
                return (width, height);
            }

            reader.BaseStream.Position += length - 2;
        }

        return null;
    }

    private static int ReadBigEndianInt32(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }

    private static int ReadBigEndianUInt16(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(2);
        return (bytes[0] << 8) | bytes[1];
    }
}
