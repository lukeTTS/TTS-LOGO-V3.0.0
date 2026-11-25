# Update configuration

The application checks a small HTTPS JSON manifest. Before release, replace the placeholder URL in `AppSettings.UpdateManifestUrl` with the permanent public location of `update.json`.

Example:

```json
{
  "version": "2.0.1",
  "installerUrl": "https://downloads.example.com/AlphaLogoUploader-2.0.1-Setup.exe",
  "sha256": "FULL_SHA256_HASH",
  "releaseNotes": "Improved printer discovery and image cropping."
}
```

The updater:

1. Compares the manifest version with the installed assembly version.
2. Downloads the installer over HTTPS.
3. Verifies the SHA-256 hash when supplied.
4. Launches the signed installer and closes the current application.

Host the manifest and installer on the same trusted company-controlled domain. Do not publish an update manifest until the installer has been code-signed and tested.
