using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AlphaLogoUploader;

public sealed record MonochromeImage(int Width, int Height, byte[] Pixels);

public sealed class ImageProcessor
{
    private BitmapSource? _source;
    public BitmapSource? OriginalBitmap => _source;
    public string? SourcePath { get; private set; }
    public int SourceWidth => _source?.PixelWidth ?? 0;
    public int SourceHeight => _source?.PixelHeight ?? 0;

    public void Load(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        _source = ConvertToBgra32(frame);
        _source.Freeze();
        SourcePath = path;
    }

    public MonochromeImage Render(int cropX, int cropY, int cropWidth, int cropHeight, int targetWidth, int targetHeight, int rotation, int threshold, bool invert, int offsetX, int offsetY, bool useCanvasSize, int canvasWidth, int canvasHeight)
    {
        if (_source is null) throw new InvalidOperationException("Choose an image first.");
        cropX = Math.Clamp(cropX, 0, _source.PixelWidth - 1);
        cropY = Math.Clamp(cropY, 0, _source.PixelHeight - 1);
        cropWidth = cropWidth <= 0 ? _source.PixelWidth - cropX : Math.Min(cropWidth, _source.PixelWidth - cropX);
        cropHeight = cropHeight <= 0 ? _source.PixelHeight - cropY : Math.Min(cropHeight, _source.PixelHeight - cropY);
        if (cropWidth < 1 || cropHeight < 1) throw new ArgumentException("The crop area must have a positive width and height.");

        BitmapSource working = new CroppedBitmap(_source, new Int32Rect(cropX, cropY, cropWidth, cropHeight));

        // Rotation is applied before sizing so the requested output dimensions
        // always describe the final printer bitmap.
        if (rotation is 90 or 180 or 270)
            working = new TransformedBitmap(working, new RotateTransform(rotation));

        int graphicWidth;
        int graphicHeight;
        if (targetWidth > 0 && targetHeight > 0)
        {
            // Treat both values as a bounding box for the graphic itself.
            var graphicScale = Math.Min(targetWidth / (double)working.PixelWidth, targetHeight / (double)working.PixelHeight);
            graphicWidth = Math.Max(1, (int)Math.Round(working.PixelWidth * graphicScale));
            graphicHeight = Math.Max(1, (int)Math.Round(working.PixelHeight * graphicScale));
        }
        else if (targetWidth > 0)
        {
            graphicWidth = targetWidth;
            graphicHeight = Math.Max(1, (int)Math.Round(working.PixelHeight * (targetWidth / (double)working.PixelWidth)));
        }
        else if (targetHeight > 0)
        {
            graphicHeight = targetHeight;
            graphicWidth = Math.Max(1, (int)Math.Round(working.PixelWidth * (targetHeight / (double)working.PixelHeight)));
        }
        else
        {
            graphicWidth = working.PixelWidth;
            graphicHeight = working.PixelHeight;
        }

        var scaleX = graphicWidth / (double)working.PixelWidth;
        var scaleY = graphicHeight / (double)working.PixelHeight;
        if (Math.Abs(scaleX - 1.0) > 0.0001 || Math.Abs(scaleY - 1.0) > 0.0001)
            working = new TransformedBitmap(working, new ScaleTransform(scaleX, scaleY));

        // The canvas is independent from the graphic. Enabling a 360 x 360
        // canvas never changes the logo dimensions; it only adds/clips whitespace.
        var outputWidth = useCanvasSize && canvasWidth > 0 ? canvasWidth : working.PixelWidth;
        var outputHeight = useCanvasSize && canvasHeight > 0 ? canvasHeight : working.PixelHeight;

        working = ConvertToBgra32(working);
        var stride = working.PixelWidth * 4;
        var bgra = new byte[stride * working.PixelHeight];
        working.CopyPixels(bgra, stride, 0);
        var converted = new byte[working.PixelWidth * working.PixelHeight];
        for (var i = 0; i < converted.Length; i++)
        {
            var b = bgra[i * 4]; var g = bgra[(i * 4) + 1]; var r = bgra[(i * 4) + 2]; var a = bgra[(i * 4) + 3];
            var gray = a == 0 ? 255 : (int)Math.Round((0.2126 * r) + (0.7152 * g) + (0.0722 * b));
            var black = gray < threshold;
            if (invert) black = !black;
            converted[i] = black ? (byte)0 : (byte)255;
        }

        var canvas = new byte[outputWidth * outputHeight];
        Array.Fill(canvas, (byte)255);

        // Centre the fitted logo, then apply the user positioning offsets.
        var originX = ((outputWidth - working.PixelWidth) / 2) + offsetX;
        var originY = ((outputHeight - working.PixelHeight) / 2) + offsetY;
        for (var y = 0; y < working.PixelHeight; y++)
        {
            var destinationY = y + originY;
            if (destinationY < 0 || destinationY >= outputHeight) continue;

            for (var x = 0; x < working.PixelWidth; x++)
            {
                var destinationX = x + originX;
                if (destinationX < 0 || destinationX >= outputWidth) continue;
                canvas[(destinationY * outputWidth) + destinationX] = converted[(y * working.PixelWidth) + x];
            }
        }

        return new MonochromeImage(outputWidth, outputHeight, canvas);
    }

    public (int X, int Y, int Width, int Height) FindInkBounds(int threshold)
    {
        if (_source is null) throw new InvalidOperationException("Choose an image first.");
        var stride = _source.PixelWidth * 4;
        var pixels = new byte[stride * _source.PixelHeight];
        _source.CopyPixels(pixels, stride, 0);
        var minX = _source.PixelWidth; var minY = _source.PixelHeight; var maxX = -1; var maxY = -1;
        for (var y = 0; y < _source.PixelHeight; y++)
        for (var x = 0; x < _source.PixelWidth; x++)
        {
            var i = (y * stride) + (x * 4);
            var b = pixels[i]; var g = pixels[i + 1]; var r = pixels[i + 2]; var a = pixels[i + 3];
            var gray = a == 0 ? 255 : (int)Math.Round((0.2126 * r) + (0.7152 * g) + (0.0722 * b));
            if (gray < threshold) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
        }
        return maxX < minX ? (0, 0, _source.PixelWidth, _source.PixelHeight) : (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public static BitmapSource ToBitmapSource(MonochromeImage image)
    {
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Gray8, null, image.Pixels, image.Width);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource ConvertToBgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Bgra32) return source;
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }
}
