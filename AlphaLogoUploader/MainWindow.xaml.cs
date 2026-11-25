using Microsoft.Win32;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace AlphaLogoUploader;

public partial class MainWindow : Window
{
    private readonly ImageProcessor _images = new();
    private readonly AppSettings _settings;
    private MonochromeImage? _rendered;
    private ZebraGraphicResult? _graphic;
    private GodexGraphicResult? _godexGraphic;
    private bool _initialising = true;

    public MainWindow()
    {
        InitializeComponent();
        // Start every application session from clean defaults.
        // Session-only edits such as filename, threshold, inversion, sizing and offsets
        // must not carry over from a previous run.
        _settings = new AppSettings();
        VersionText.Text = $"Version {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
        ApplySettings();
        ApplyEquipmentProfile(updatePreview: false);
        if (!string.IsNullOrWhiteSpace(_settings.GraphicName))
            GraphicNameText.Text = _settings.GraphicName;
        if (!IsGodex && string.Equals(_settings.ZebraFileFormat, "PCX", StringComparison.OrdinalIgnoreCase))
            UploadTargetCombo.SelectedIndex = 1;
        UpdateUploadTargetUi();
        RefreshPrinters();
        _initialising = false;
        UpdateConnectionUi();
        PersistCheck.IsEnabled = !IsGodex;
        GraphicFileHint.Text = IsGodex ? "GoDEX file: custlogo.bmp (stored in user flash)" : "Zebra file: custlogo.GRF";
        SaveGraphicButton.Content = IsGodex ? "Save BMP file…" : "Save Zebra file…";
        UpdatePcxButtons();
        UpdateUploadTargetUi();
        SizePresetHint.Text = IsGodex
            ? "GoDEX Alpha canvas: 360 × 360 px; graphic size remains independent."
            : "Canvas size is optional and does not resize the graphic.";
    }

    private void ApplySettings()
    {
        GraphicNameText.Text = "custlogo";
        EquipmentProfileCombo.SelectedIndex = _settings.EquipmentProfile switch
        {
            EquipmentProfile.Alpha3340Godex => 0,
            EquipmentProfile.Metrel3309Godex => 2,
            EquipmentProfile.SeawardZebra => 3,
            _ => 1
        };
        PrinterTypeCombo.SelectedIndex = _settings.EquipmentProfile == EquipmentProfile.Alpha3340Zebra ? 0 : 1;
        ThresholdSlider.Value = _settings.Threshold;
        InvertCheck.IsChecked = _settings.Invert;
        PersistCheck.IsChecked = _settings.PersistFlash;
        TargetWidthText.Text = _settings.TargetWidth.ToString();
        TargetHeightText.Text = _settings.TargetHeight.ToString();
        UseCanvasSizeCheck.IsChecked = _settings.UseCanvasSize;
        CanvasWidthText.Text = (_settings.CanvasWidth > 0 ? _settings.CanvasWidth : 360).ToString();
        CanvasHeightText.Text = (_settings.CanvasHeight > 0 ? _settings.CanvasHeight : 360).ToString();
        UpdateCanvasControls();
        OffsetXText.Text = _settings.OffsetX.ToString();
        OffsetYText.Text = _settings.OffsetY.ToString();
        RotationCombo.SelectedIndex = _settings.Rotation switch { 0 => 0, 90 => 1, 180 => 2, 270 => 3, _ => 1 };
        QueueModeRadio.IsChecked = _settings.ConnectionMode == ConnectionMode.WindowsQueue;
        IpModeRadio.IsChecked = _settings.ConnectionMode == ConnectionMode.NetworkIp;
        IpText.Text = _settings.PrinterIp ?? "";
        PortText.Text = _settings.PrinterPort.ToString();
    }

    private AppSettings CaptureSettings() => new()
    {
        LastImageFolder = _images.SourcePath is null ? _settings.LastImageFolder : Path.GetDirectoryName(_images.SourcePath),
        EquipmentProfile = CurrentEquipmentProfile,
        PrinterType = IsGodex ? PrinterType.GodexRt200 : PrinterType.ZebraZq52x,
        GraphicName = GraphicNameText.Text.Trim(),
        ZebraFileFormat = ZebraUploadTarget,
        Rotation = RotationValue(), Threshold = (int)ThresholdSlider.Value,
        Invert = InvertCheck.IsChecked == true, PersistFlash = PersistCheck.IsChecked == true,
        TargetWidth = Parse(TargetWidthText.Text), TargetHeight = Parse(TargetHeightText.Text),
        UseCanvasSize = UseCanvasSizeCheck.IsChecked == true,
        CanvasWidth = Parse(CanvasWidthText.Text, 360), CanvasHeight = Parse(CanvasHeightText.Text, 360),
        OffsetX = Parse(OffsetXText.Text), OffsetY = Parse(OffsetYText.Text),
        ConnectionMode = IpModeRadio.IsChecked == true ? ConnectionMode.NetworkIp : ConnectionMode.WindowsQueue,
        PrinterName = PrinterCombo.SelectedItem as string, PrinterIp = IpText.Text.Trim(), PrinterPort = Parse(PortText.Text, 9100),
        UpdateManifestUrl = _settings.UpdateManifestUrl
    };

    private EquipmentProfile CurrentEquipmentProfile
    {
        get
        {
            if (EquipmentProfileCombo.SelectedItem is ComboBoxItem item)
            {
                return item.Tag?.ToString() switch
                {
                    "AlphaGodex" => EquipmentProfile.Alpha3340Godex,
                    "M3309Godex" => EquipmentProfile.Metrel3309Godex,
                    "SeawardZebra" => EquipmentProfile.SeawardZebra,
                    _ => EquipmentProfile.Alpha3340Zebra
                };
            }
            return EquipmentProfile.Alpha3340Zebra;
        }
    }

    private void EquipmentProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _initialising) return;
        ApplyEquipmentProfile(updatePreview: true);
        RefreshPrinters();
    }

    private void ApplyEquipmentProfile(bool updatePreview)
    {
        var profile = CurrentEquipmentProfile;
        var isZebra = profile is EquipmentProfile.Alpha3340Zebra or EquipmentProfile.SeawardZebra;
        var isSeaward = profile == EquipmentProfile.SeawardZebra;
        var isAlphaGodex = profile == EquipmentProfile.Alpha3340Godex;

        // Each equipment profile starts from its own clean defaults so temporary
        // changes made for one printer/tester combination do not leak into another.
        PrinterTypeCombo.SelectedIndex = isZebra ? 0 : 1;
        UploadTargetCombo.SelectedIndex = isSeaward ? 1 : 0;
        GraphicNameText.Text = isSeaward ? "PASS" : "custlogo";
        PersistCheck.IsChecked = true;

        ThresholdSlider.Value = 128;
        InvertCheck.IsChecked = false;
        RotationCombo.SelectedIndex = 1; // 90 degrees
        TargetWidthText.Text = "0";
        TargetHeightText.Text = "0";
        OffsetXText.Text = "0";
        OffsetYText.Text = "0";
        CanvasWidthText.Text = "360";
        CanvasHeightText.Text = "360";

        if (_images.SourcePath is not null)
            ResetCropFields();

        if (isAlphaGodex)
        {
            UseCanvasSizeCheck.IsChecked = true;
            CanvasWidthText.Text = "360";
            CanvasHeightText.Text = "360";
            ProfileTitleText.Text = "Alpha 3340 + GoDEX configuration selected";
            ProfileDescriptionText.Text = "The app will create and upload the selected .BMP filename using a 360 × 360 printer canvas.";
        }
        else if (profile == EquipmentProfile.Alpha3340Zebra)
        {
            UseCanvasSizeCheck.IsChecked = false;
            ProfileTitleText.Text = "Alpha 3340 + Zebra ZQ521 configuration selected";
            ProfileDescriptionText.Text = "The app will create and upload the selected .GRF filename to the Zebra printer.";
        }
        else if (isSeaward)
        {
            UseCanvasSizeCheck.IsChecked = false;
            ProfileTitleText.Text = "Seaward + Zebra ZQ configuration selected";
            ProfileDescriptionText.Text = "The app will create and upload a native .PCX file. PASS and FAIL are common names, but any valid filename may be used.";
        }
        else
        {
            UseCanvasSizeCheck.IsChecked = false;
            ProfileTitleText.Text = "Metrel 3309BT + GoDEX configuration selected";
            ProfileDescriptionText.Text = "The app will create and upload the selected .BMP filename at the selected logo size.";
        }

        UploadTargetCombo.IsEnabled = isZebra;
        UpdateCanvasControls();
        UpdateProfileSummary();
        PersistCheck.IsEnabled = isZebra;
        SaveGraphicButton.Content = isZebra ? "Save printer file…" : "Save BMP file…";
        SizePresetHint.Text = isAlphaGodex
            ? "Automatically set to 360 × 360 for the Metrel Alpha 3340 with GoDEX."
            : "Canvas is not required for this equipment setup.";
        UpdatePcxButtons();
        UpdateUploadTargetUi();

        if (updatePreview && _images.SourcePath is not null)
            RefreshPreview();
    }

    private void UpdateProfileSummary()
    {
        var name = NormalizedGraphicBaseName();
        var extension = IsGodex ? ".bmp" : ZebraUploadTarget == "PCX" ? ".PCX" : ".GRF";
        if (UploadSummaryText is null || PrinterCombo is null || IpText is null || PortText is null) return;
        var filename = IsGodex ? _godexGraphic?.FileName ?? name + extension : ZebraUploadTarget == "PCX" ? name.ToUpperInvariant() + extension : (_graphic?.Name ?? name) + extension;
        var destination = IpModeRadio.IsChecked == true ? $"{IpText.Text}:{PortText.Text}" : PrinterCombo.SelectedItem as string ?? "Not selected";
        var size = _rendered is null ? "No logo prepared" : $"{_rendered.Width} × {_rendered.Height} px";
        UploadSummaryText.Text = $"Equipment: {EquipmentProfileCombo.Text}\nPrinter: {destination}\nFilename: {filename}\nImage size: {size}";
        var validDestination = IpModeRadio.IsChecked == true ? !string.IsNullOrWhiteSpace(IpText.Text) && int.TryParse(PortText.Text, out var port) && port is > 0 and <= 65535 : PrinterCombo.SelectedItem is string;
        UploadButton.IsEnabled = _graphic is not null && _godexGraphic is not null && validDestination;
        SaveGraphicButton.IsEnabled = _graphic is not null && _godexGraphic is not null;
        LogoStatusText.Text = _rendered is null ? "Choose or prepare a valid logo." : validDestination ? "Logo prepared. Confirm the printer before uploading." : "Logo prepared. Select a printer.";
    }

    private void ChooseImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a customer logo",
            Filter = "Supported images|*.png;*.jpg;*.jpeg;*.bmp|PNG images|*.png|JPEG images|*.jpg;*.jpeg|Bitmap images|*.bmp",
            InitialDirectory = Directory.Exists(_settings.LastImageFolder) ? _settings.LastImageFolder : null
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            _images.Load(dialog.FileName);
            OriginalImage.Source = _images.OriginalBitmap;
            OriginalPlaceholder.Visibility = Visibility.Collapsed;
            ImagePathText.Text = dialog.FileName;
            ResetCropFields();
            RefreshPreview();
            SetStatus("Logo loaded successfully.");
        }
        catch (Exception ex) { ShowError("The image could not be opened", ex.Message); }
    }

    private void EditorChanged(object sender, EventArgs e)
    {
        if (!_initialising && IsLoaded) UpdateUploadTargetUi();
        if (!_initialising && _images.SourcePath is not null) RefreshPreview();
    }

    private void RefreshPreview()
    {
        try
        {
            _rendered = _images.Render(
                Parse(CropXText.Text), Parse(CropYText.Text), Parse(CropWidthText.Text), Parse(CropHeightText.Text),
                Parse(TargetWidthText.Text), Parse(TargetHeightText.Text), RotationValue(), (int)ThresholdSlider.Value,
                InvertCheck.IsChecked == true, Parse(OffsetXText.Text), Parse(OffsetYText.Text),
                UseCanvasSizeCheck.IsChecked == true, Parse(CanvasWidthText.Text, 360), Parse(CanvasHeightText.Text, 360));
            _graphic = ZebraCompression.Build(_rendered, GraphicNameText.Text, PersistCheck.IsChecked == true);
            _godexGraphic = GodexBitmap.Build(_rendered, GraphicNameText.Text);
            PreviewImage.Source = ImageProcessor.ToBitmapSource(_rendered);
            PreviewPlaceholder.Visibility = Visibility.Collapsed;
            ImageInfoText.Text = $"Source: {_images.SourceWidth} × {_images.SourceHeight}px";
            OutputInfoText.Text = IsGodex
                ? $"Output: {_rendered.Width} × {_rendered.Height}px • {_godexGraphic.BmpData.Length:N0}-byte 1-bit BMP • {_godexGraphic.FileName}"
                : $"Output: {_rendered.Width} × {_rendered.Height}px • {_graphic.TotalBytes:N0} uncompressed bytes • {_graphic.CompressedData.Length:N0} compressed characters";
        }
        catch (Exception ex)
        {
            _graphic = null;
            _godexGraphic = null;
            _rendered = null;
            PreviewImage.Source = null;
            PreviewPlaceholder.Visibility = Visibility.Visible;
            OutputInfoText.Text = ex.Message;
        }
        UpdateProfileSummary();
    }

    private void GodexSizePreset_Click(object sender, RoutedEventArgs e)
    {
        UseCanvasSizeCheck.IsChecked = true;
        CanvasWidthText.Text = "360";
        CanvasHeightText.Text = "360";
        UpdateCanvasControls();
        if (_images.SourcePath is not null) RefreshPreview();
        SetStatus("Canvas set to 360 × 360 px. The graphic size was not changed.");
    }

    private void CanvasOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateCanvasControls();
        if (!_initialising && _images.SourcePath is not null) RefreshPreview();
    }

    private void UpdateCanvasControls()
    {
        var enabled = UseCanvasSizeCheck.IsChecked == true;
        CanvasWidthText.IsEnabled = enabled;
        CanvasHeightText.IsEnabled = enabled;
    }

    private void AutoCrop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var b = _images.FindInkBounds((int)ThresholdSlider.Value);
            CropXText.Text = b.X.ToString(); CropYText.Text = b.Y.ToString(); CropWidthText.Text = b.Width.ToString(); CropHeightText.Text = b.Height.ToString();
            RefreshPreview();
        }
        catch (Exception ex) { ShowError("Auto-crop failed", ex.Message); }
    }

    private void ResetCrop_Click(object sender, RoutedEventArgs e) { ResetCropFields(); RefreshPreview(); }
    private void ResetCropFields() { CropXText.Text = "0"; CropYText.Text = "0"; CropWidthText.Text = _images.SourceWidth.ToString(); CropHeightText.Text = _images.SourceHeight.ToString(); }

    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureGraphic()) return;
        var isPcx = !IsGodex && ZebraUploadTarget == "PCX";
        var pcxName = NormalizedGraphicBaseName().ToUpperInvariant() + ".PCX";
        var dialog = IsGodex
            ? new SaveFileDialog { Filter = "Monochrome bitmap|*.bmp", FileName = _godexGraphic!.FileName }
            : isPcx
                ? new SaveFileDialog { Filter = "PCX image|*.pcx", FileName = pcxName, AddExtension = true, DefaultExt = ".pcx" }
                : new SaveFileDialog { Filter = "Zebra graphic payload|*.zpl", FileName = $"{_graphic!.Name}.zpl" };
        if (dialog.ShowDialog() != true) return;
        File.WriteAllBytes(dialog.FileName, IsGodex ? _godexGraphic!.BmpData : isPcx ? ZebraPcx.Build(_rendered!) : _graphic!.ToBridgePayload());
        SetStatus($"Saved {Path.GetFileName(dialog.FileName)}.");
    }


    private void UpdatePcxButtons()
    {
        SavePassPcxButton.Visibility = Visibility.Collapsed;
        SaveFailPcxButton.Visibility = Visibility.Collapsed;
        UploadTargetLabel.Visibility = Visibility.Collapsed;
        UploadTargetCombo.Visibility = Visibility.Visible;
    }

    private string ZebraUploadTarget
    {
        get
        {
            if (UploadTargetCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                return tag;
            return "GRF";
        }
    }

    private string NormalizedGraphicBaseName()
    {
        var raw = Path.GetFileNameWithoutExtension(GraphicNameText.Text.Trim());
        var cleaned = new string(raw.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "custlogo";
        return cleaned.Length > 16 ? cleaned[..16] : cleaned;
    }

    private void UploadTargetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) UpdateUploadTargetUi();
    }

    private void UpdateUploadTargetUi()
    {
        var name = NormalizedGraphicBaseName();
        var isPcx = !IsGodex && ZebraUploadTarget == "PCX";
        UploadButton.Content = "Upload logo to printer";
        UploadTargetCombo.IsEnabled = !IsGodex;
        FileNameHintText.Text = IsGodex
            ? $"The printer will store {name}.bmp. GoDEX names are limited to 16 characters."
            : isPcx
                ? $"The printer will store {name.ToUpperInvariant()}.PCX. PASS and FAIL are common Seaward filenames, but other names are supported."
                : $"The printer will store {name}.GRF. Enter a different name when the tester or label format expects another graphic.";
        GraphicFileHint.Text = IsGodex
            ? $"Output file: {name}.bmp (stored in GoDEX user flash)"
            : isPcx ? $"Output file: {name.ToUpperInvariant()}.PCX" : $"Output file: {name}.GRF";
        UpdateProfileSummary();
    }

    private void SavePassPcx_Click(object sender, RoutedEventArgs e) => SaveSeawardPcx("PASS.PCX");

    private void SaveFailPcx_Click(object sender, RoutedEventArgs e) => SaveSeawardPcx("FAIL.PCX");

    private void SaveSeawardPcx(string fileName)
    {
        if (!EnsureGraphic() || _rendered is null) return;
        if (IsGodex)
        {
            ShowError("PCX export unavailable", "PASS.PCX and FAIL.PCX are available for Zebra / Seaward use only.");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = $"Save {fileName} for a Seaward tester",
            Filter = "PCX image|*.pcx",
            FileName = fileName,
            AddExtension = true,
            DefaultExt = ".pcx"
        };
        if (dialog.ShowDialog() != true) return;

        File.WriteAllBytes(dialog.FileName, ZebraPcx.Build(_rendered));
        SetStatus($"Saved {Path.GetFileName(dialog.FileName)} for Seaward.");
    }

    private void SaveDebug_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureGraphic()) return;
        var fileName = IsGodex ? $"{_godexGraphic!.GraphicName}_command.txt" : $"{_graphic!.Name}_debug.txt";
        var dialog = new SaveFileDialog { Filter = "Text file|*.txt", FileName = fileName };
        if (dialog.ShowDialog() != true) return;
        File.WriteAllText(dialog.FileName, IsGodex ? _godexGraphic!.ToCommandSummary() : _graphic!.ToTextCommand());
        SetStatus($"Saved {Path.GetFileName(dialog.FileName)}.");
    }

    private void RefreshPrinters_Click(object sender, RoutedEventArgs e) => RefreshPrinters();

    private void RefreshPrinters()
    {
        try
        {
            var all = PrinterService.ListInstalledPrinters();
            var matched = all.Where(p => IsGodex
                ? p.Contains("GoDEX", StringComparison.OrdinalIgnoreCase) || p.Contains("Godex", StringComparison.OrdinalIgnoreCase) || p.Contains("RT200", StringComparison.OrdinalIgnoreCase)
                : p.Contains("Zebra", StringComparison.OrdinalIgnoreCase) || p.Contains("ZQ52", StringComparison.OrdinalIgnoreCase)).ToArray();
            var shown = matched.Length > 0 ? matched : all.ToArray();
            PrinterCombo.ItemsSource = shown;
            PrinterCombo.SelectedItem = shown.FirstOrDefault(p => p.Equals(_settings.PrinterName, StringComparison.OrdinalIgnoreCase)) ?? (matched.Length > 0 ? matched.FirstOrDefault() : null);
            var label = IsGodex ? "GoDEX RT200" : "Zebra";
            SetStatus(matched.Length > 0 ? $"Found {matched.Length} {label} printer queue(s)." : shown.Length > 0 ? $"No {label}-named queue was found; showing all installed printers." : "No installed printer queues were found.");
        }
        catch (Exception ex) { ShowError("Printer detection failed", ex.Message); }
    }

    private bool IsGodex => PrinterTypeCombo.SelectedIndex == 1;

    private void PrinterTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        PersistCheck.IsEnabled = !IsGodex;
        GraphicFileHint.Text = IsGodex ? "GoDEX file: custlogo.bmp (stored in user flash)" : "Zebra file: custlogo.GRF";
        SaveGraphicButton.Content = IsGodex ? "Save BMP file…" : "Save Zebra file…";
        UpdatePcxButtons();
        UpdateUploadTargetUi();
        SizePresetHint.Text = IsGodex
            ? "GoDEX Alpha canvas: 360 × 360 px; graphic size remains independent."
            : "Canvas size is optional and does not resize the graphic.";
        UpdateCanvasControls();
        UpdateProfileSummary();
        RefreshPrinters();
        if (_images.SourcePath is not null) RefreshPreview();
    }

    private void ConnectionModeChanged(object sender, RoutedEventArgs e) { if (IsLoaded) UpdateConnectionUi(); }
    private void UpdateConnectionUi() { PrinterCombo.IsEnabled = QueueModeRadio.IsChecked == true; IpText.IsEnabled = PortText.IsEnabled = IpModeRadio.IsChecked == true; UpdateProfileSummary(); }

    private void DestinationChanged(object sender, RoutedEventArgs e) { if (!_initialising && IsLoaded) UpdateProfileSummary(); }

    private void Help_Click(object sender, RoutedEventArgs e) => MessageBox.Show("1. Select your tester and printer.\n2. Choose a logo and check the monochrome preview.\n3. Select the correct Windows queue or printer IP address. Test the connection, then upload.\n4. Print a test tag from your appliance tester to verify the logo.\n\nA successful transmission does not confirm that the printer stored the graphic.\nFor support visit testandtagsupplies.com.au.", "Printer Logo Uploader help", MessageBoxButton.OK, MessageBoxImage.Information);

    private async void Diagnostic_Click(object sender, RoutedEventArgs e)
    {
        await Busy(async () =>
        {
            PrinterDiagnosticResult result;
            if (IpModeRadio.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(IpText.Text)) throw new InvalidOperationException("Enter the printer IP address first.");
                result = await PrinterService.DiagnoseTcpAsync(IpText.Text.Trim(), Parse(PortText.Text, 9100), queryZebra: !IsGodex);
            }
            else
            {
                if (PrinterCombo.SelectedItem is not string printer) throw new InvalidOperationException("Select an installed printer queue first.");
                result = PrinterService.DiagnoseQueue(printer);
            }
            ToolsExpander.IsExpanded = true;
            DiagnosticText.Text = $"{result.Summary}\r\n\r\n{result.Details}";
            SetStatus(result.Summary);
        });
    }

    private async void Upload_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureGraphic()) return;
        await Busy(async () =>
        {
            var zebraTarget = ZebraUploadTarget;
            var pcxFileName = NormalizedGraphicBaseName().ToUpperInvariant() + ".PCX";
            var payload = IsGodex
                ? _godexGraphic!.ToUploadPayload()
                : zebraTarget == "GRF"
                    ? _graphic!.ToBridgePayload()
                    : ZebraPcx.BuildUploadPayload(_rendered!, pcxFileName, PersistCheck.IsChecked == true);
            var jobName = IsGodex
                ? $"Upload {_godexGraphic!.FileName}"
                : zebraTarget == "GRF" ? $"Upload {_graphic!.Name}.GRF" : $"Upload {pcxFileName}";
            if (IpModeRadio.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(IpText.Text)) throw new InvalidOperationException("Enter the printer IP address first.");
                await PrinterService.SendTcpAsync(IpText.Text.Trim(), Parse(PortText.Text, 9100), payload);
            }
            else
            {
                if (PrinterCombo.SelectedItem is not string printer) throw new InvalidOperationException("Select an installed printer queue first.");
                await Task.Run(() => PrinterService.SendRawQueue(printer, payload, jobName));
            }
            DiagnosticText.Text = IsGodex
                ? $"Upload sent.\r\n\r\nGraphic: {_godexGraphic!.FileName}\r\nBMP size: {_godexGraphic.BmpData.Length:N0} bytes\r\nOutput size: {_rendered!.Width} × {_rendered.Height}px\r\n\r\nThe app first requests deletion of an existing file with the same name, then sends the EZPL ~EB download command and binary BMP data."
                : zebraTarget == "GRF"
                    ? $"Upload completed.\r\n\r\nGraphic: {_graphic!.DevicePrefix}{_graphic.Name}.GRF\r\nPayload: {payload.Length:N0} bytes\r\nOutput size: {_rendered!.Width} × {_rendered.Height}px"
                    : $"Upload completed.\r\n\r\nGraphic: {(PersistCheck.IsChecked == true ? "E:" : "R:")}{pcxFileName}\r\nFormat: native monochrome PCX\r\nPayload: {payload.Length:N0} bytes\r\nOutput size: {_rendered!.Width} × {_rendered.Height}px";
            var uploadedName = IsGodex ? _godexGraphic!.FileName : zebraTarget == "GRF" ? $"{_graphic!.Name}.GRF" : pcxFileName;
            SetStatus($"{uploadedName} upload was sent.");
            MessageBox.Show($"The {uploadedName} upload was sent successfully. Print a test tag from your appliance tester to verify the logo.", "Logo sent – verification required", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        await Busy(async () =>
        {
            var manifest = await UpdateService.CheckAsync(_settings.UpdateManifestUrl);
            if (manifest is null) { MessageBox.Show("Update checking is ready, but the update-manifest URL has not been configured yet. See docs/UPDATES.md.", "Updates not configured", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            if (!UpdateService.IsNewer(manifest.Version)) { MessageBox.Show("You already have the current version.", "No update available", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var answer = MessageBox.Show($"Version {manifest.Version} is available.\r\n\r\n{manifest.ReleaseNotes}\r\n\r\nDownload and start the installer now?", "Update available", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
            var installer = await UpdateService.DownloadInstallerAsync(manifest, new Progress<double>(v => SetStatus($"Downloading update… {v:N0}%")));
            UpdateService.LaunchInstaller(installer);
            Application.Current.Shutdown();
        });
    }

    private bool EnsureGraphic()
    {
        if (_images.SourcePath is null) { ShowError("No logo selected", "Choose a PNG, JPG or BMP logo first."); return false; }
        RefreshPreview();
        if (_graphic is null || _godexGraphic is null) { ShowError("Logo conversion failed", OutputInfoText.Text); return false; }
        return true;
    }

    private async Task Busy(Func<Task> action)
    {
        BusyProgress.Visibility = Visibility.Visible;
        IsEnabled = false;
        try { await action(); }
        catch (Exception ex) { ShowError("Operation failed", ex.Message); }
        finally { IsEnabled = true; BusyProgress.Visibility = Visibility.Collapsed; }
    }

    private int RotationValue() => RotationCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out var value) ? value : 90;
    private static int Parse(string? value, int fallback = 0) => int.TryParse(value, out var result) ? result : fallback;
    private void SetStatus(string text) => StatusText.Text = text;
    private static void ShowError(string title, string details) => MessageBox.Show(details, title, MessageBoxButton.OK, MessageBoxImage.Error);

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        // Deliberately do not persist session settings. Every new launch starts
        // with the same defaults as a freshly selected equipment profile.
    }
}
