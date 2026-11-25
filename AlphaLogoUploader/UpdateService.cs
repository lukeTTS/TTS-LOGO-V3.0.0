using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace AlphaLogoUploader;

public static class UpdateService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static async Task<UpdateManifest?> CheckAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Contains("example.com", StringComparison.OrdinalIgnoreCase)) return null;
        var json = await Http.GetStringAsync(url, ct);
        return JsonSerializer.Deserialize<UpdateManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public static bool IsNewer(string version) => Version.TryParse(version, out var remote) && remote > typeof(UpdateService).Assembly.GetName().Version;

    public static async Task<string> DownloadInstallerAsync(UpdateManifest manifest, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var target = Path.Combine(Path.GetTempPath(), $"AlphaLogoUploader-{manifest.Version}-Setup.exe");
        using var response = await Http.GetAsync(manifest.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var dest = File.Create(target);
        var buffer = new byte[81920]; long readTotal = 0; int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, read), ct); readTotal += read;
            if (total > 0) progress?.Report(readTotal * 100d / total.Value);
        }
        await dest.FlushAsync(ct);
        if (!string.IsNullOrWhiteSpace(manifest.Sha256))
        {
            dest.Close();
            using var file = File.OpenRead(target);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
            if (!hash.Equals(manifest.Sha256.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)) { File.Delete(target); throw new InvalidDataException("The downloaded update failed its SHA-256 integrity check."); }
        }
        return target;
    }

    public static void LaunchInstaller(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
}
