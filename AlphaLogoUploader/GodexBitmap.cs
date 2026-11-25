namespace AlphaLogoUploader;

public static class GodexBitmap
{
    public static GodexGraphicResult Build(MonochromeImage image, string requestedName)
    {
        var baseName = Path.GetFileNameWithoutExtension(requestedName?.Trim());
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "custlogo";
        var invalid = Path.GetInvalidFileNameChars();
        baseName = new string(baseName.Where(c => !invalid.Contains(c) && (char.IsLetterOrDigit(c) || c is '_' or '-')).ToArray());
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "custlogo";
        if (baseName.Length > 16) baseName = baseName[..16]; // leaves room for .bmp within GoDEX's 20-char limit
        var fileName = baseName + ".bmp";
        return new GodexGraphicResult(fileName, Encode1BppBmp(image));
    }

    public static byte[] Encode1BppBmp(MonochromeImage image)
    {
        if (image.Width <= 0 || image.Height <= 0) throw new ArgumentException("The image must have a positive size.");
        var rowBytes = (image.Width + 7) / 8;
        var paddedRowBytes = (rowBytes + 3) & ~3;
        var pixelBytes = paddedRowBytes * image.Height;
        const int fileHeaderSize = 14;
        const int dibHeaderSize = 40;
        const int paletteSize = 8;
        var pixelOffset = fileHeaderSize + dibHeaderSize + paletteSize;
        var fileSize = pixelOffset + pixelBytes;
        var output = new byte[fileSize];

        void W16(int o, ushort v) { output[o] = (byte)v; output[o + 1] = (byte)(v >> 8); }
        void W32(int o, int v) { output[o] = (byte)v; output[o + 1] = (byte)(v >> 8); output[o + 2] = (byte)(v >> 16); output[o + 3] = (byte)(v >> 24); }

        output[0] = (byte)'B'; output[1] = (byte)'M';
        W32(2, fileSize); W32(10, pixelOffset);
        W32(14, dibHeaderSize); W32(18, image.Width); W32(22, image.Height);
        W16(26, 1); W16(28, 1); W32(34, pixelBytes); W32(38, 3780); W32(42, 3780); W32(46, 2); W32(50, 2);
        // Palette index 0 = black, index 1 = white (BGRA)
        output[54] = 0; output[55] = 0; output[56] = 0; output[57] = 0;
        output[58] = 255; output[59] = 255; output[60] = 255; output[61] = 0;

        for (var outputRow = 0; outputRow < image.Height; outputRow++)
        {
            var sourceY = image.Height - 1 - outputRow; // standard BMP is bottom-up
            var dest = pixelOffset + outputRow * paddedRowBytes;
            for (var x = 0; x < image.Width; x++)
            {
                var isWhite = image.Pixels[sourceY * image.Width + x] != 0;
                if (isWhite) output[dest + (x / 8)] |= (byte)(0x80 >> (x % 8));
            }
        }
        return output;
    }
}
