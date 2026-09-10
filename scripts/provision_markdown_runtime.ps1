<#
.SYNOPSIS
Provisions the isolated, app-owned Markdown runtime for Zlet Converter.

.DESCRIPTION
Installs an app-owned Python 3.11 environment with pinned Docling dependencies
from evaluation/requirements-lock.txt into the designated app-owned runtime directory
(default: %LocalAppData%\Zlet Converter\runtimes\markdown).
Copies worker script and prepares offline layout models so conversion runs 100% offline.

.PARAMETER Destination
Target directory for the app-owned runtime. Defaults to %LocalAppData%\Zlet Converter\runtimes\markdown.

.PARAMETER PythonBase
Base Python 3.11 executable used to create the virtual environment. Defaults to python or evaluation/venv if present.

.PARAMETER FromEvaluationVenv
If specified and evaluation/venv exists, clones dependencies directly from evaluation/venv for fast local setup.

.PARAMETER VerifyOffline
Runs offline verification after provisioning to ensure no remote network calls occur.
#>
[CmdletBinding()]
param(
    [string]$Destination = "$env:LOCALAPPDATA\Zlet Converter\runtimes\markdown",
    [string]$PythonBase = "",
    [switch]$FromEvaluationVenv,
    [switch]$VerifyOffline
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$workerSource = Join-Path $repoRoot "src\Zlet.FolderConverter.DoclingWorker\zlet_docling_worker.py"
$lockFile = Join-Path $repoRoot "evaluation\requirements-lock.txt"
$evalVenv = Join-Path $repoRoot "evaluation\venv"

Write-Host "=== Zlet Converter: Provisioning Markdown Runtime ===" -ForegroundColor Cyan
Write-Host "Destination: $Destination"

# Ensure destination directory exists
if (!(Test-Path $Destination)) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
}

$destPython = Join-Path $Destination "Scripts\python.exe"
$destPythonAlt = Join-Path $Destination "python.exe"

if ($FromEvaluationVenv -and (Test-Path (Join-Path $evalVenv "Scripts\python.exe"))) {
    Write-Host "Provisioning from local evaluation venv..." -ForegroundColor Yellow
    Copy-Item -Path (Join-Path $evalVenv "*") -Destination $Destination -Recurse -Force
} else {
    # Resolve base Python 3.11
    if ([string]::IsNullOrWhiteSpace($PythonBase)) {
        if (Test-Path (Join-Path $evalVenv "Scripts\python.exe")) {
            $PythonBase = Join-Path $evalVenv "Scripts\python.exe"
        } else {
            $pyCmd = Get-Command "python" -ErrorAction SilentlyContinue
            if ($pyCmd) {
                $PythonBase = $pyCmd.Source
            } else {
                throw "Python 3.11 executable not found. Please specify -PythonBase path."
            }
        }
    }

    Write-Host "Creating virtual environment using: $PythonBase"
    & $PythonBase -m venv $Destination
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create virtual environment in $Destination"
    }

    $pipExe = Join-Path $Destination "Scripts\pip.exe"
    Write-Host "Installing pinned dependencies from $lockFile..." -ForegroundColor Yellow
    & $pipExe install --no-cache-dir -r $lockFile
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install dependencies from $lockFile"
    }
}

# Copy worker script to runtime root
Write-Host "Copying worker script..." -ForegroundColor Yellow
Copy-Item -Path $workerSource -Destination (Join-Path $Destination "zlet_docling_worker.py") -Force

# Prepare models directory inside runtime for offline execution
$modelsDir = Join-Path $Destination "models"
if (!(Test-Path $modelsDir)) {
    New-Item -ItemType Directory -Path $modelsDir -Force | Out-Null
}

# If user/eval HF cache exists, copy cached models
$hfCache = Join-Path $env:USERPROFILE ".cache\huggingface\hub"
if (Test-Path $hfCache) {
    Write-Host "Copying cached layout models into runtime models directory..." -ForegroundColor Yellow
    $destHfHub = Join-Path $modelsDir "hub"
    if (!(Test-Path $destHfHub)) {
        New-Item -ItemType Directory -Path $destHfHub -Force | Out-Null
    }
    Copy-Item -Path (Join-Path $hfCache "*") -Destination $destHfHub -Recurse -Force
}

# Generate runtime manifest
$manifest = @{
    component = "zlet-markdown-runtime"
    version = "1.0.0"
    targetDoclingVersion = "2.126.0"
    targetPythonVersion = "3.11"
    createdAt = (Get-Date).ToString("o")
    offlineReady = (Test-Path (Join-Path $modelsDir "hub"))
    dependencies = @{
        docling = "2.126.0"
        openpyxl = "3.1.5"
        beautifulsoup4 = "4.15.0"
    }
} | ConvertTo-Json -Depth 5

Set-Content -Path (Join-Path $Destination "runtime_manifest.json") -Value $manifest -Encoding UTF8

Write-Host "Runtime manifest generated at: $(Join-Path $Destination 'runtime_manifest.json')" -ForegroundColor Green

$resolvedPython = if (Test-Path $destPython) { $destPython } else { $destPythonAlt }
$workerScript = Join-Path $Destination "zlet_docling_worker.py"
$proc = Start-Process -FilePath $resolvedPython -ArgumentList "`"$workerScript`"" -RedirectStandardInput (New-TemporaryFile) -RedirectStandardOutput (New-TemporaryFile) -PassThru -NoNewWindow
Start-Sleep -Milliseconds 500
if (!$proc.HasExited) {
    $proc.Kill()
}

if ($VerifyOffline) {
    Write-Host "Running offline verification..." -ForegroundColor Yellow
    $verifyScript = Join-Path $repoRoot "evaluation\verify_offline.py"
    if (Test-Path $verifyScript) {
        $env:HF_HOME = $modelsDir
        & $resolvedPython $verifyScript
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Offline verification: PASSED" -ForegroundColor Green
        } else {
            Write-Warning "Offline verification: FAILED (Exit Code: $LASTEXITCODE)"
        }
    }
}

Write-Host "=== Markdown Runtime Provisioning Complete ===" -ForegroundColor Green
