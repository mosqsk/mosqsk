#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Removes the Virtual ZPL Printer and its TCP/IP port from Windows.

.PARAMETER PrinterName
    Name of the printer to remove. Default: "Virtual ZPL Printer"

.PARAMETER PortNumber
    Port number used when the printer was installed. Default: 9100
#>

param(
    [string] $PrinterName = "Virtual ZPL Printer",
    [int]    $PortNumber  = 9100
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PortName = "VirtualZPL_$PortNumber"

function Write-Step([string]$msg) { Write-Host "  >> $msg" -ForegroundColor Cyan }
function Write-OK([string]$msg)   { Write-Host "  OK  $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "  !!  $msg" -ForegroundColor Yellow }

Write-Host "`nVirtual ZPL Printer — Uninstaller" -ForegroundColor White
Write-Host "===================================" -ForegroundColor DarkGray

# ---- 1. Remove printer ------------------------------------------------------
Write-Step "Removing printer '$PrinterName'..."
$existing = Get-Printer -Name $PrinterName -ErrorAction SilentlyContinue
if ($existing) {
    Remove-Printer -Name $PrinterName
    Write-OK "Printer removed."
} else {
    Write-Warn "Printer '$PrinterName' not found — skipping."
}

# ---- 2. Remove port ---------------------------------------------------------
Write-Step "Removing port '$PortName'..."
$existingPort = Get-PrinterPort -Name $PortName -ErrorAction SilentlyContinue
if ($existingPort) {
    Remove-PrinterPort -Name $PortName
    Write-OK "Port removed."
} else {
    Write-Warn "Port '$PortName' not found — skipping."
}

Write-Host ""
Write-Host "Done!  Virtual ZPL Printer has been uninstalled." -ForegroundColor Green
Write-Host ""
