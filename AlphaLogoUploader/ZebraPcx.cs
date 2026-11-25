namespace AlphaLogoUploader;

/// <summary>
/// Creates a standard monochrome PCX file suitable for importing into
/// Seaward testers. PCX scan lines are padded to an even byte count and
/// encoded with the format's byte-level run-length encoding.
/// </summary>
public static class ZebraPcx
{
    public static byte[] Build(MonochromeImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Width <= 0 || image.Height <= 0)
            throw new ArgumentException("The rendered image has no usable dimensions.", nameof(image));
        if (image.Width > ushort.MaxValue || image.Height > ushort.MaxValue)
            throw new ArgumentException("The rendered image is too large for PCX format.", nameof(image));

        var bytesPerLine = ((image.Width + 7) / 8 + 1) & ~1;
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, System.Text.Encoding.ASCII, leaveOpen: true);

        // 128-byte PCX header.
        writer.Write((byte)0x0A);                         // Manufacturer: ZSoft PCX
        writer.Write((byte)5);                            // Version 3.0+
        writer.Write((byte)1);                            // PCX RLE encoding
        writer.Write((byte)1);                            // 1 bit per pixel per plane
        writer.Write((ushort)0);                          // Xmin
        writer.Write((ushort)0);                          // Ymin
        writer.Write((ushort)(image.Width - 1));          // Xmax
        writer.Write((ushort)(image.Height - 1));         // Ymax
        writer.Write((ushort)image.Width);                // Horizontal DPI
        writer.Write((ushort)image.Height);               // Vertical DPI

        // 16-colour header palette. For 1-bit PCX, use the conventional
        // mapping expected by Seaward/Zebra: palette index 0 is black and
        // palette index 1 is white. This prevents BMP artwork from appearing
        // inverted after conversion to PCX.
        writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0);
        writer.Write((byte)255); writer.Write((byte)255); writer.Write((byte)255);
        writer.Write(new byte[42]);

        writer.Write((byte)0);                            // Reserved
        writer.Write((byte)1);                            // Colour planes
        writer.Write((ushort)bytesPerLine);               // Bytes per scan line (even)
        writer.Write((ushort)1);                          // Palette type: colour/monochrome
        writer.Write((ushort)image.Width);                // Screen width
        writer.Write((ushort)image.Height);               // Screen height
        writer.Write(new byte[54]);                       // Filler to byte 128

        var row = new byte[bytesPerLine];
        for (var y = 0; y < image.Height; y++)
        {
            Array.Clear(row);
            for (var x = 0; x < image.Width; x++)
            {
                // MonochromeImage uses 0 for black and 255 for white.
                // In a 1-bit PCX, bit 0 maps to palette index 0 (black) and
                // bit 1 maps to palette index 1 (white), so only white pixels
                // need their bit set.
                if (image.Pixels[(y * image.Width) + x] != 0)
                    row[x / 8] |= (byte)(0x80 >> (x % 8));
            }
            WriteRle(writer, row);
        }

        writer.Flush();
        return output.ToArray();
    }


    public static byte[] BuildUploadPayload(MonochromeImage image, string fileName, bool persistFlash = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A PCX filename is required.", nameof(fileName));

        var rawName = Path.GetFileNameWithoutExtension(fileName).Trim().ToUpperInvariant();
        var baseName = new string(rawName.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
        if (string.IsNullOrWhiteSpace(baseName))
            throw new ArgumentException("A valid PCX filename is required.", nameof(fileName));
        if (baseName.Length > 16) baseName = baseName[..16];

        var device = persistFlash ? "E:" : "R:";
        var pcx = Build(image);

        using var output = new MemoryStream();
        // Remove an older object with the same name, then download the native PCX.
        var deleteCommand = System.Text.Encoding.ASCII.GetBytes($"^XA^ID{device}{baseName}.PCX^FS^XZ");
        output.Write(deleteCommand);

        // ~DY destination:name, binary input, PCX extension, byte count, bytes/row ignored for PCX.
        var header = System.Text.Encoding.ASCII.GetBytes($"~DY{device}{baseName},B,X,{pcx.Length},0,");
        output.Write(header);
        output.Write(pcx);
        return output.ToArray();
    }

    private static void WriteRle(BinaryWriter writer, ReadOnlySpan<byte> data)
    {
        for (var i = 0; i < data.Length;)
        {
            var value = data[i];
            var count = 1;
            while (i + count < data.Length && data[i + count] == value && count < 63)
                count++;

            if (count > 1 || (value & 0xC0) == 0xC0)
            {
                writer.Write((byte)(0xC0 | count));
                writer.Write(value);
            }
            else
            {
                writer.Write(value);
            }
            i += count;
        }
    }
}
