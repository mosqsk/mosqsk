using System.Text;
using VirtualPrinter.Core.Models;

namespace VirtualPrinter.Core.Services;

/// <summary>
/// Processes a received print job:
///   1. Saves the raw ZPL/binary data to disk.
///   2. Optionally requests a PNG preview from the Labelary API.
/// </summary>
public sealed class ZplProcessor : IDisposable
{
    private readonly PrinterConfiguration _config;
    private readonly HttpClient _http;

    public event EventHandler<string>? LogMessage;

    public ZplProcessor(PrinterConfiguration config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task ProcessAsync(PrintJob job)
    {
        job.Status = PrintJobStatus.Processing;

        try
        {
            if (_config.SaveJobsToFile)
                await SaveRawAsync(job);

            if (_config.EnableZplRendering && job.IsZpl)
                await RenderPreviewAsync(job);

            job.Status = PrintJobStatus.Completed;
        }
        catch (Exception ex)
        {
            job.Status = PrintJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            Log($"Error processing job {job.Id}: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Save raw ZPL
    // -------------------------------------------------------------------------

    private async Task SaveRawAsync(PrintJob job)
    {
        Directory.CreateDirectory(_config.JobOutputDirectory);

        var fileName = $"{job.ReceivedAt:yyyyMMdd_HHmmss}_{job.Id.ToString("N")[..8]}.zpl";
        var filePath = Path.Combine(_config.JobOutputDirectory, fileName);

        await File.WriteAllBytesAsync(filePath, job.Data);
        job.SavedFilePath = filePath;

        Log($"Saved job to {filePath}");
    }

    // -------------------------------------------------------------------------
    // ZPL rendering via Labelary API
    // -------------------------------------------------------------------------

    private async Task RenderPreviewAsync(PrintJob job)
    {
        try
        {
            var zpl = Encoding.UTF8.GetString(job.Data);
            using var content = new StringContent(zpl, Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await _http.PostAsync(_config.LabelaryUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                Log($"Labelary returned {(int)response.StatusCode} — skipping preview.");
                return;
            }

            var imageBytes = await response.Content.ReadAsByteArrayAsync();

            // Store preview alongside the raw file, or in a separate previews sub-folder
            var basePath = job.SavedFilePath is not null
                ? Path.ChangeExtension(job.SavedFilePath, ".png")
                : Path.Combine(_config.JobOutputDirectory, "previews",
                    $"{job.ReceivedAt:yyyyMMdd_HHmmss}_{job.Id.ToString("N")[..8]}.png");

            Directory.CreateDirectory(Path.GetDirectoryName(basePath)!);
            await File.WriteAllBytesAsync(basePath, imageBytes);
            job.PreviewImagePath = basePath;

            Log($"Preview saved to {basePath}");
        }
        catch (HttpRequestException ex)
        {
            // Labelary is optional — don't fail the whole job
            Log($"Labelary preview unavailable: {ex.Message}");
        }
    }

    private void Log(string msg) => LogMessage?.Invoke(this, msg);

    public void Dispose() => _http.Dispose();
}
