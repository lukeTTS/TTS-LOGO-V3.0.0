# Build the Windows application using GitHub

This project includes `.github/workflows/build-windows.yml`. GitHub supplies the Windows build computer, .NET 8 SDK and Inno Setup, so Visual Studio is not required on your computer.

## First-time setup

1. Sign in to GitHub and create a new **private** repository.
2. Extract this project ZIP.
3. Upload the **contents** of the `AlphaLogoUploaderNative` folder to the root of the repository. The repository root should contain `AlphaLogoUploaderNative.sln`, `AlphaLogoUploader`, `installer`, `tools` and `.github`.
4. Commit the uploaded files.

GitHub may hide the `.github` folder in some local file browsers because its name begins with a dot. It must be included in the upload.

## Build the application

1. Open the repository on GitHub.
2. Select **Actions**.
3. Select **Build Windows Release**.
4. Select **Run workflow**.
5. Enter a version such as `2.0.1`.
6. Select the green **Run workflow** button.
7. Open the completed workflow run.
8. Under **Artifacts**, download `AlphaLogoUploader-2.0.1-Windows`.

The downloaded artifact contains:

- `AlphaLogoUploader-2.0.1.exe` — the self-contained application.
- `AlphaLogoUploader-2.0.1-Setup.exe` — the Windows installer.
- `AlphaLogoUploader-2.0.1-Portable-win-x64.zip` — a portable package.
- `SHA256SUMS.txt` — integrity hashes for the generated files.

## Build from a version tag

The same workflow runs automatically when a tag matching `v*` is pushed, such as `v2.0.1`. For normal use, the manual **Run workflow** button is simpler.

## Windows security warning

These builds are unsigned until a code-signing certificate is configured. Windows SmartScreen may display an unknown-publisher warning. This does not prevent internal testing, but a trusted Authenticode certificate is recommended before public customer distribution.
