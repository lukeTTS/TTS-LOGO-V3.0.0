using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace AlphaLogoUploader;

public static class PrinterService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PRINTER_INFO_4 { public string pPrinterName; public string pServerName; public uint Attributes; }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool EnumPrinters(uint flags, string? name, uint level, IntPtr buffer, uint size, out uint needed, out uint returned);
    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool OpenPrinter(string printerName, out IntPtr printer, IntPtr defaults);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool ClosePrinter(IntPtr printer);
    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)] private static extern uint StartDocPrinter(IntPtr printer, uint level, ref DOC_INFO_1 docInfo);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndDocPrinter(IntPtr printer);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool StartPagePrinter(IntPtr printer);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool EndPagePrinter(IntPtr printer);
    [DllImport("winspool.drv", SetLastError = true)] private static extern bool WritePrinter(IntPtr printer, byte[] bytes, uint count, out uint written);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOC_INFO_1 { public string pDocName; public string? pOutputFile; public string pDataType; }

    public static IReadOnlyList<string> ListInstalledPrinters(bool zebraOnly = false)
    {
        const uint local = 2, connections = 4;
        EnumPrinters(local | connections, null, 4, IntPtr.Zero, 0, out var needed, out _);
        if (needed == 0) return Array.Empty<string>();
        var buffer = Marshal.AllocHGlobal((int)needed);
        try
        {
            if (!EnumPrinters(local | connections, null, 4, buffer, needed, out _, out var returned))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            var size = Marshal.SizeOf<PRINTER_INFO_4>();
            var printers = new List<string>();
            for (var i = 0; i < returned; i++)
            {
                var item = Marshal.PtrToStructure<PRINTER_INFO_4>(IntPtr.Add(buffer, i * size));
                if (!zebraOnly || item.pPrinterName.Contains("Zebra", StringComparison.OrdinalIgnoreCase) || item.pPrinterName.Contains("ZQ52", StringComparison.OrdinalIgnoreCase))
                    printers.Add(item.pPrinterName);
            }
            return printers.OrderBy(x => x).ToArray();
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    public static void SendRawQueue(string printerName, byte[] data, string jobName)
    {
        if (!OpenPrinter(printerName, out var printer, IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not open the Windows printer queue.");
        try
        {
            var doc = new DOC_INFO_1 { pDocName = jobName, pDataType = "RAW", pOutputFile = null };
            if (StartDocPrinter(printer, 1, ref doc) == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not start a raw print job.");
            try
            {
                if (!StartPagePrinter(printer)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                try
                {
                    if (!WritePrinter(printer, data, (uint)data.Length, out var written) || written != data.Length)
                        throw new IOException($"Only {written} of {data.Length} bytes were written to the printer queue.");
                }
                finally { EndPagePrinter(printer); }
            }
            finally { EndDocPrinter(printer); }
        }
        finally { ClosePrinter(printer); }
    }

    public static async Task SendTcpAsync(string ip, int port, byte[] data, CancellationToken ct = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(ip, port, ct);
        await using var stream = client.GetStream();
        await stream.WriteAsync(data, ct);
        await stream.FlushAsync(ct);
    }

    public static async Task<PrinterDiagnosticResult> DiagnoseTcpAsync(string ip, int port, CancellationToken ct = default, bool queryZebra = true)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(ip, port, timeout.Token);
            await using var stream = client.GetStream();
            if (!queryZebra) return new(true, "Network connection successful", "TCP port accepted the connection. Printer identity and readiness are not verified.");
            var query = Encoding.ASCII.GetBytes("~HI\r\n");
            await stream.WriteAsync(query, timeout.Token);
            await stream.FlushAsync(timeout.Token);
            var response = new byte[2048];
            var readTask = stream.ReadAsync(response, timeout.Token).AsTask();
            var completed = await Task.WhenAny(readTask, Task.Delay(1500, timeout.Token));
            var details = completed == readTask && readTask.Result > 0 ? Encoding.ASCII.GetString(response, 0, readTask.Result).Trim() : "Port 9100 accepted the connection; the printer did not return identification text.";
            return new(true, "Network connection successful", details);
        }
        catch (Exception ex) { return new(false, "Network diagnostic failed", ExplainNetworkError(ex, ip, port)); }
    }

    public static PrinterDiagnosticResult DiagnoseQueue(string printerName)
    {
        try
        {
            var exists = ListInstalledPrinters().Contains(printerName, StringComparer.OrdinalIgnoreCase);
            return exists
                ? new(true, "Windows queue is available", $"The queue ‘{printerName}’ is installed. Use Upload Test to verify end-to-end raw printing.")
                : new(false, "Windows queue was not found", "Refresh printers or install the Zebra Windows driver before using queue printing.");
        }
        catch (Exception ex) { return new(false, "Queue diagnostic failed", ex.Message); }
    }

    private static string ExplainNetworkError(Exception ex, string ip, int port) => ex switch
    {
        OperationCanceledException => $"Connection to {ip}:{port} timed out. Confirm the printer is powered on, connected to the same network and using the correct IP address.",
        SocketException sx => $"Windows could not connect to {ip}:{port}. Socket error {sx.SocketErrorCode}. Check the address, Wi-Fi connection, firewall and RAW port setting.",
        _ => ex.Message
    };
}
