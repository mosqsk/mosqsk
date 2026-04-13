#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs the Virtual ZPL Printer as a Windows TCP/IP printer.

.DESCRIPTION
    Creates a RAW TCP/IP printer port pointing at 127.0.0.1:9100 and adds a
    Windows printer using the built-in "Generic / Text Only" driver.
    The resulting printer is visible in Devices & Printers and will be
    discovered by the D365 FnO Document Routing Agent.

.PARAMETER PrinterName
    Name of the printer to create. Default: "Virtual ZPL Printer"

.PARAMETER PortNumber
    TCP port the Virtual ZPL Printer application listens on. Default: 9100

.EXAMPLE
    .\Install-Printer.ps1
    .\Install-Printer.ps1 -PrinterName "My ZPL Printer" -PortNumber 9101
#>

param(
    [string] $PrinterName = "Virtual ZPL Printer",
    [int]    $PortNumber  = 9100
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PortName   = "VirtualZPL_$PortNumber"
$DriverName = "Generic / Text Only"
$HostAddr   = "127.0.0.1"

function Write-Step([string]$msg) { Write-Host "  >> $msg" -ForegroundColor Cyan }
function Write-OK([string]$msg)   { Write-Host "  OK  $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "  !!  $msg" -ForegroundColor Yellow }

Write-Host "`nVirtual ZPL Printer — Windows Installer" -ForegroundColor White
Write-Host "========================================" -ForegroundColor DarkGray

# ---- 1. Verify driver -------------------------------------------------------
Write-Step "Checking for '$DriverName' printer driver..."
$driver = Get-PrinterDriver -Name $DriverName -ErrorAction SilentlyContinue
if (-not $driver) {
    Write-Warn "Driver '$DriverName' not found. Attempting to install..."
    # The Generic driver ships with Windows; Add-PrinterDriver will pull it from the driver store.
    Add-PrinterDriver -Name $DriverName
    Write-OK "Driver installed."
} else {
    Write-OK "Driver found."
}

# ---- 2. Create TCP/IP port --------------------------------------------------
Write-Step "Creating printer port '$PortName' -> $HostAddr`:$PortNumber ..."
$existingPort = Get-PrinterPort -Name $PortName -ErrorAction SilentlyContinue
if ($existingPort) {
    Write-Warn "Port '$PortName' already exists — skipping."
} else {
    Add-PrinterPort -Name $PortName -PrinterHostAddress $HostAddr -PortNumber $PortNumber
    Write-OK "Port created."
}

# ---- 3. Install printer -----------------------------------------------------
Write-Step "Installing printer '$PrinterName'..."
$existing = Get-Printer -Name $PrinterName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Warn "Printer '$PrinterName' already exists — skipping."
} else {
    Add-Printer -Name $PrinterName -DriverName $DriverName -PortName $PortName -Comment "Virtual ZPL Printer for D365 FnO DRA"
    Write-OK "Printer installed."
}

# ---- Done -------------------------------------------------------------------
Write-Host ""
Write-Host "Done!  '$PrinterName' is now available in Devices & Printers." -ForegroundColor Green
Write-Host "Start the VirtualPrinter.exe application before printing." -ForegroundColor Gray
Write-Host ""
