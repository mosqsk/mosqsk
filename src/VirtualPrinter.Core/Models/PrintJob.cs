using System.Text;

namespace VirtualPrinter.Core.Models;

public class PrintJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime ReceivedAt { get; init; } = DateTime.Now;
    public string ClientAddress { get; init; } = string.Empty;
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public int SizeBytes => Data.Length;
    public PrintJobStatus Status { get; set; } = PrintJobStatus.Received;
    public string? ErrorMessage { get; set; }
    public string? SavedFilePath { get; set; }
    public string? PreviewImagePath { get; set; }

    public string ZplContent => Encoding.UTF8.GetString(Data);

    public bool IsZpl => ZplContent.TrimStart().StartsWith("^XA", StringComparison.OrdinalIgnoreCase)
                      || ZplContent.TrimStart().StartsWith("^xa", StringComparison.OrdinalIgnoreCase);
}

public enum PrintJobStatus
{
    Received,
    Processing,
    Completed,
    Failed
}
