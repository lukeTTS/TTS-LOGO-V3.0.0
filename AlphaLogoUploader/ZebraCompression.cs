namespace AlphaLogoUploader;

public static class ZebraCompression
{
    private const string OneToNineteen = " GHIJKLMNOPQRSTUVWXY";
    private const string TwentySteps = "ghijklmnopqrstuvwxyz";

    private static string EncodeRepeat(int count)
    {
        if (count <= 1) return string.Empty;
        var output = new System.Text.StringBuilder();
        while (count >= 400) { output.Append('z'); count -= 400; }
        var twenties = count / 20;
        if (twenties > 0) { output.Append(TwentySteps[twenties - 1]); count -= twenties * 20; }
        if (count > 0) output.Append(OneToNineteen[count]);
        return output.ToString();
    }

    private static string Rle(string value)
    {
        var output = new System.Text.StringBuilder();
        for (var i = 0; i < value.Length;)
        {
            var j = i + 1;
            while (j < value.Length && value[j] == value[i]) j++;
            var run = j - i;
            if (run > 1) output.Append(EncodeRepeat(run));
            output.Append(value[i]);
            i = j;
        }
        return output.ToString();
    }

    private static string CompressRow(string row)
    {
        var best = Rle(row);
        foreach (var candidate in new[] { ('0', ','), ('F', '!') })
        {
            for (var cut = 0; cut < row.Length; cut++)
            {
                var allFill = true;
                for (var k = cut; k < row.Length; k++) if (row[k] != candidate.Item1) { allFill = false; break; }
                if (allFill)
                {
                    var compressed = Rle(row[..cut]) + candidate.Item2;
                    if (compressed.Length < best.Length) best = compressed;
                    break;
                }
            }
        }
        return best;
    }

    public static ZebraGraphicResult Build(MonochromeImage monochrome, string name, bool persistFlash)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Graphic name is required.");
        name = new string(name.Trim().Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
        if (name.Length == 0) throw new ArgumentException("Graphic name contains no valid characters.");

        var bytesPerRow = (monochrome.Width + 7) / 8;
        var packed = new byte[bytesPerRow * monochrome.Height];
        for (var y = 0; y < monochrome.Height; y++)
        for (var x = 0; x < monochrome.Width; x++)
            if (monochrome.Pixels[(y * monochrome.Width) + x] == 0)
                packed[(y * bytesPerRow) + (x / 8)] |= (byte)(0x80 >> (x % 8));

        var compressed = new System.Text.StringBuilder();
        string? previous = null;
        for (var y = 0; y < monochrome.Height; y++)
        {
            var row = Convert.ToHexString(packed.AsSpan(y * bytesPerRow, bytesPerRow));
            if (previous == row) compressed.Append(':');
            else { compressed.Append(CompressRow(row)); previous = row; }
        }
        return new ZebraGraphicResult(persistFlash ? "e:" : "r:", name, packed.Length, bytesPerRow, compressed.ToString());
    }
}
