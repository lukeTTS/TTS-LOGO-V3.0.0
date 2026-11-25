# TTS Printer Logo Uploader 3.0.0

Native Windows replacement for the original Python/Tkinter Zebra Graphic Converter.

## Included features

- Native Windows WPF application written in C#.
- Self-contained single-file build; customers do not install Python or .NET.
- PNG, JPG, JPEG and BMP input.
- Crop coordinates, automatic white-border crop, proportional resize, 0/90/180/270 rotation, inversion and adjustable black/white threshold.
- Pixel-accurate monochrome preview using the same rendered bitmap sent to the printer.
- Existing Zebra Bridge-style binary `DG` payload format retained.
- Persistent `e:` or temporary `r:` graphic storage.
- Automatic detection of installed Zebra/ZQ52x Windows printer queues, with fallback to all installed queues.
- RAW Windows queue printing for USB or installed network printers.
- Direct RAW TCP/IP printing, default port 9100.
- Network and queue diagnostics with clearer error messages.
- Clean session defaults on every launch; edits are not persisted.
- Update implementation retained internally; the customer update button is hidden pending secure configuration.
- Inno Setup installer and Authenticode signing scripts.

## Building without Visual Studio

The recommended build method is the included GitHub Actions workflow. GitHub creates the Windows EXE, portable ZIP and Inno Setup installer on a hosted Windows machine. Only a GitHub account and web browser are required.

See [`docs/GITHUB_BUILD.md`](docs/GITHUB_BUILD.md).

## Optional local development requirements

- Windows 10 or Windows 11.
- Visual Studio with .NET desktop development, or the .NET 8 SDK.
- Inno Setup 6 for the installer.
- Windows SDK `signtool.exe` and a code-signing certificate for signed releases.

## Build

Open `AlphaLogoUploaderNative.sln` in Visual Studio, or run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\tools\build-release.ps1
```

The self-contained executable is written to `publish`. The installer is written to `installer\output` when Inno Setup is installed.

## Signing

```powershell
.\tools\sign-release.ps1 -CertificateThumbprint "YOUR_CERTIFICATE_THUMBPRINT"
```

Signing cannot be completed without the publisher's private code-signing certificate.

## Update publishing

Configure the permanent manifest URL in `AppSettings.UpdateManifestUrl`, build and sign the release, upload the installer, then run:

```powershell
.\tools\make-update-manifest.ps1 `
  -Version "2.0.1" `
  -InstallerUrl "https://your-domain.example/downloads/AlphaLogoUploader-2.0.1-Setup.exe" `
  -ReleaseNotes "Release notes here."
```

See `docs/UPDATES.md` and `docs/CODE_SIGNING.md`.

## Important testing

Before customer distribution, test with the actual Metrel Alpha 3340 and Zebra ZQ521 using:

- A USB-installed Zebra queue.
- A network-installed Windows queue.
- Direct IP port 9100.
- Flash persistence after printer restart.
- Replacement of an existing `custlogo.GRF`.
- Logos with widths not divisible by eight.


## GoDEX RT200 support

Select **GoDEX RT200** in the app. The processed logo is encoded as a standards-compliant 1-bit monochrome `custlogo.bmp`, then sent as RAW data using EZPL's `~EB,name,size` graphic-download command. The app sends `~MDELG,name` first so an existing graphic can be replaced. USB uses the installed Windows GoDEX printer queue; Ethernet uses RAW TCP port 9100.

## GoDEX Alpha sizing and TTS branding

- Output width and height can now be set independently.
- Selecting **GoDEX RT200** automatically applies the required **360 × 360 px** Alpha logo canvas.
- The **Set GoDEX 360 × 360** button reapplies the preset at any time.
- Logos are scaled proportionally and centred within the requested canvas; X/Y offsets can then fine-tune their position.
- The interface uses the supplied TTS colour palette: Vibrant Red `#CC0000`, Seafoam `#99CCCC`, Charcoal `#312F2F`, Beige `#E5E6DC`, and White `#F7F7F7`.


## Separate canvas sizing (v2.3.0)

The graphic dimensions and printer canvas dimensions can now be controlled independently. Enable **Use a separate canvas size** and set the canvas to **360 × 360 px** for GoDEX Alpha output. This adds or clips whitespace around the graphic without rescaling the graphic itself. The X/Y offset controls position the unchanged graphic within the canvas.
