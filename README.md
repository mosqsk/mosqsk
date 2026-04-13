# Virtual ZPL Printer for D365 FnO

A virtual ZPL/label printer for Windows that integrates with **Microsoft Dynamics 365 Finance & Operations** Document Routing Agent (DRA). Installs as a real Windows printer — visible in *Devices & Printers* and discoverable by the DRA — without requiring physical Zebra hardware.

## How it works

```
D365 FnO  ──►  Document Routing Agent  ──►  Windows Printer  ──►  VirtualPrinter.exe  ──►  ZPL file + PNG preview
```

1. The app opens a TCP listener on port **9100** (configurable).
2. It installs a Windows **TCP/IP printer port** pointing at `127.0.0.1:9100`.
3. A Windows printer backed by that port appears in *Devices & Printers*.
4. The D365 FnO Document Routing Agent discovers and routes label jobs to it.
5. Every received job is saved as a `.zpl` file and, optionally, rendered to a `.png` preview via the [Labelary](http://labelary.com) API.

## Key improvements over Virtual-ZPL-Printer

| Feature | Virtual-ZPL-Printer | This project |
|---------|---------------------|--------------|
| Visible in Devices & Printers | No (TCP only) | **Yes** |
| D365 DRA compatible | Manual setup | **Auto-install** |
| Windows printer install/remove | No | **Built-in UI + PowerShell** |
| ZPL preview rendering | Yes | Yes (Labelary) |
| System tray | No | **Yes** |
| Settings UI | No | **Yes** |

## Requirements

- Windows 10/11 or Windows Server 2019+
- .NET 8.0 Runtime
- **Administrator privileges** (for printer installation)

## Quick start

### Option A — application UI

1. Run `VirtualPrinter.exe` as Administrator.
2. Click **Install Printer** — creates *"Virtual ZPL Printer"* in Windows.
3. Server starts automatically on port 9100.
4. In D365 FnO, open the Document Routing Agent and add *"Virtual ZPL Printer"*.

### Option B — PowerShell (headless / automated)

```powershell
# Install (run as Administrator)
.\scripts\Install-Printer.ps1

# Then start the application (server only, no UI interaction needed)
.\VirtualPrinter.exe

# Uninstall
.\scripts\Uninstall-Printer.ps1
```

## D365 FnO Document Routing Agent setup

1. Install and configure the DRA on the same Windows machine.
2. Open the DRA application → **Add printer** → select *"Virtual ZPL Printer"*.
3. In D365 FnO go to **Organization administration › Document management › Network printers**.
4. Refresh the list — *Virtual ZPL Printer* should appear.
5. Assign it to your label printing configuration or print management document type.

## Project structure

```
src/
├── VirtualPrinter.sln
├── VirtualPrinter.Core/          # .NET 8 class library
│   ├── PrinterServer.cs          # TCP listener
│   ├── Models/
│   │   ├── PrintJob.cs
│   │   └── PrinterConfiguration.cs
│   └── Services/
│       ├── WindowsPrinterManager.cs   # WMI printer install/remove
│       └── ZplProcessor.cs            # Save ZPL + call Labelary
└── VirtualPrinter.App/           # .NET 8 WinForms executable
    ├── Program.cs
    ├── VirtualPrinterAppContext.cs
    ├── TrayManager.cs
    ├── Forms/
    │   ├── MainForm.cs            # Main window
    │   └── SettingsForm.cs        # Settings dialog
    └── appsettings.json

scripts/
├── Install-Printer.ps1
└── Uninstall-Printer.ps1
```

## Configuration (`appsettings.json`)

| Key | Default | Description |
|-----|---------|-------------|
| `ListenPort` | `9100` | TCP port to accept print jobs on |
| `ListenAddress` | `0.0.0.0` | Bind address (`0.0.0.0` = all interfaces) |
| `PrinterName` | `Virtual ZPL Printer` | Windows printer display name |
| `PortName` | `VirtualZPL_9100` | Windows printer port name |
| `SaveJobsToFile` | `true` | Save raw ZPL data to `JobOutputDirectory` |
| `EnableZplRendering` | `true` | Render PNG preview via Labelary |
| `LabelDensity` | `8dpmm` | Label density (`8dpmm` = 203 dpi, `12dpmm` = 300 dpi) |
| `LabelWidth` | `4` | Label width in inches |
| `LabelHeight` | `6` | Label height in inches |
| `StartMinimized` | `false` | Start minimized to system tray |
| `MaxJobHistory` | `100` | Number of jobs shown in the UI list |

## Building

```bash
cd src
dotnet build VirtualPrinter.sln -c Release
```

Output: `src/VirtualPrinter.App/bin/Release/net8.0-windows/`

## Acknowledgements

Inspired by [Virtual-ZPL-Printer](https://github.com/porrey/Virtual-ZPL-Printer) by Daniel Porrey.  
ZPL rendering powered by [Labelary](http://labelary.com).
