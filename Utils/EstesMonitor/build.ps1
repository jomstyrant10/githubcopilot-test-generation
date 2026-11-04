$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { throw "C# compiler not found: $csc" }

$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$engine = Join-Path $PSScriptRoot 'Engine.cs'
$webExt = "/reference:$(Join-Path (Split-Path $csc) 'System.Web.Extensions.dll')"
$zipRef = '/reference:System.IO.Compression.dll'

# Console app (headless / scheduled tasks)
$exe = Join-Path $dist 'EstesMonitor.exe'
& $csc /nologo /target:exe /optimize+ /platform:anycpu "/out:$exe" $webExt $zipRef $engine (Join-Path $PSScriptRoot 'Cli.cs')
if ($LASTEXITCODE -ne 0) { throw "Console build failed (exit $LASTEXITCODE)" }

# Window app (no console window); written as .new.exe if the running copy is locked
$guiExe = Join-Path $dist 'EstesMonitorUI.exe'
if (Test-Path -LiteralPath $guiExe) {
    try { [IO.File]::Open($guiExe, 'Open', 'ReadWrite', 'None').Dispose() }
    catch { $guiExe = Join-Path $dist 'EstesMonitorUI.new.exe'; Write-Warning "EstesMonitorUI.exe is running; building $guiExe instead. Close the window and rename it." }
}
& $csc /nologo /target:winexe /optimize+ /platform:anycpu "/out:$guiExe" $webExt $zipRef /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $engine (Join-Path $PSScriptRoot 'Gui.cs')
if ($LASTEXITCODE -ne 0) { throw "UI build failed (exit $LASTEXITCODE)" }

& $exe --write-config --force | Out-Null
Write-Output "Built: $exe"
Write-Output "Built: $guiExe"
Write-Output "Config: $(Join-Path $dist 'estes-monitor.config.json')"
