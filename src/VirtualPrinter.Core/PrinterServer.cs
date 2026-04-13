using System.Net;
using System.Net.Sockets;
using VirtualPrinter.Core.Models;

namespace VirtualPrinter.Core;

/// <summary>
/// TCP listener that receives raw/ZPL print jobs on a configurable port.
/// Designed to be the backend for a Windows TCP/IP printer port pointing at 127.0.0.1.
/// </summary>
public sealed class PrinterServer : IDisposable
{
    private readonly PrinterConfiguration _config;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public event EventHandler<PrintJob>? JobReceived;
    public event EventHandler<string>? LogMessage;
    public event EventHandler<Exception>? ErrorOccurred;

    public bool IsRunning { get; private set; }
    public int Port => _config.ListenPort;

    public PrinterServer(PrinterConfiguration config)
    {
        _config = config;
    }

    public Task StartAsync()
    {
        if (IsRunning)
            return Task.CompletedTask;

        _cts = new CancellationTokenSource();

        var bindAddress = _config.ListenAddress == "0.0.0.0"
            ? IPAddress.Any
            : IPAddress.Parse(_config.ListenAddress);

        _listener = new TcpListener(bindAddress, _config.ListenPort);
        _listener.Start();
        IsRunning = true;

        Log($"Server started — listening on port {_config.ListenPort}");

        return Task.Run(() => AcceptLoopAsync(_cts.Token), _cts.Token);
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _cts?.Cancel();
        _listener?.Stop();
        IsRunning = false;

        Log("Server stopped");
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleClientAsync(client, token), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken token)
    {
        var clientAddress = "unknown";

        try
        {
            if (client.Client.RemoteEndPoint is IPEndPoint ep)
                clientAddress = ep.Address.ToString();

            using (client)
            using var stream = client.GetStream();
            using var ms = new MemoryStream();

            var buffer = new byte[8192];
            stream.ReadTimeout = 3000; // ms — short timeout; clients disconnect after sending

            try
            {
                int read;
                while ((read = await stream.ReadAsync(buffer, token)) > 0)
                    await ms.WriteAsync(buffer.AsMemory(0, read), token);
            }
            catch (IOException) { } // Normal: client closed connection

            if (ms.Length == 0) return;

            var job = new PrintJob
            {
                ClientAddress = clientAddress,
                Data = ms.ToArray()
            };

            Log($"Received {job.SizeBytes:N0} bytes from {clientAddress}");
            JobReceived?.Invoke(this, job);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    private void Log(string message) =>
        LogMessage?.Invoke(this, message);

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _cts?.Dispose();
        _disposed = true;
    }
}
