namespace AlphaLogoUploader;

public enum ConnectionMode
{
    WindowsQueue,
    NetworkIp
}

public enum PrinterType
{
    ZebraZq52x,
    GodexRt200
}

public enum EquipmentProfile
{
    Alpha3340Godex,
    Alpha3340Zebra,
    Metrel3309Godex,
    SeawardZebra
}

public sealed class AppSettings
{
    public EquipmentProfile EquipmentProfile { get; set; } = EquipmentProfile.Alpha3340Zebra;
    public string? LastImageFolder { get; set; }
    public PrinterType PrinterType { get; set; } = PrinterType.ZebraZq52x;
    public string GraphicName { get; set; } = "custlogo";
    public string ZebraFileFormat { get; set; } = "GRF";
    public int Rotation { get; set; } = 90;
    public int Threshold { get; set; } = 128;
    public bool Invert { get; set; }
    public bool PersistFlash { get; set; } = true;
    public int TargetWidth { get; set; }
    public int TargetHeight { get; set; }

    // Optional printer canvas. When enabled, the graphic keeps its own size
    // and is centred/positioned inside this separate output canvas.
    public bool UseCanvasSize { get; set; }
    public int CanvasWidth { get; set; } = 360;
    public int CanvasHeight { get; set; } = 360;

    // Logo positioning inside the output bitmap.
    // Positive X moves right; negative X moves left.
    // Positive Y moves down; negative Y moves up.
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }

    public ConnectionMode ConnectionMode { get; set; } = ConnectionMode.WindowsQueue;
    public string? PrinterName { get; set; }
    public string? PrinterIp { get; set; }
    public int PrinterPort { get; set; } = 9100;
    public string UpdateManifestUrl { get; set; } =
        "https://example.com/alpha-logo-uploader/update.json";
}

public sealed record ZebraGraphicResult(
    string DevicePrefix,
    string Name,
    int TotalBytes,
    int BytesPerRow,
    string CompressedData)
{
    public byte[] ToBridgePayload()
    {
        using var ms = new MemoryStream();

        ms.WriteByte(0x10);
        ms.Write(Encoding.ASCII.GetBytes("DG"));
        ms.Write(Encoding.ASCII.GetBytes($"{DevicePrefix}{Name}.GRF"));
        ms.WriteByte(0x1F);
        ms.Write(Encoding.ASCII.GetBytes(TotalBytes.ToString()));
        ms.WriteByte(0x1F);
        ms.Write(Encoding.ASCII.GetBytes(BytesPerRow.ToString()));
        ms.WriteByte(0x1F);
        ms.Write(Encoding.ASCII.GetBytes(CompressedData));

        return ms.ToArray();
    }

    public string ToTextCommand()
    {
        return $"~DG{DevicePrefix}{Name}.GRF,{TotalBytes},{BytesPerRow},{CompressedData}";
    }
}

public sealed record PrinterDiagnosticResult(
    bool Success,
    string Summary,
    string Details);

public sealed record UpdateManifest(
    string Version,
    string InstallerUrl,
    string? Sha256,
    string? ReleaseNotes);

public sealed record GodexGraphicResult(
    string FileName,
    byte[] BmpData)
{
    public string GraphicName =>
        Path.GetFileNameWithoutExtension(FileName);

    public byte[] ToDeletePayload()
    {
        return Encoding.ASCII.GetBytes($"~MDELG,{GraphicName}\r");
    }

    public byte[] ToDownloadPayload()
    {
        using var ms = new MemoryStream();

        ms.Write(Encoding.ASCII.GetBytes(
            $"~EB,{GraphicName},{BmpData.Length}\r"));

        ms.Write(BmpData);

        return ms.ToArray();
    }

    public byte[] ToUploadPayload(bool deleteExisting = true)
    {
        using var ms = new MemoryStream();

        if (deleteExisting)
        {
            ms.Write(ToDeletePayload());
        }

        ms.Write(ToDownloadPayload());

        return ms.ToArray();
    }

    public string ToCommandSummary(bool deleteExisting = true)
    {
        var deleteCommand = deleteExisting
            ? $"~MDELG,{GraphicName}<CR>\r\n"
            : string.Empty;

        return
            $"{deleteCommand}" +
            $"~EB,{GraphicName},{BmpData.Length}<CR>\r\n" +
            $"[followed immediately by {BmpData.Length:N0} binary BMP bytes]";
    }
}
