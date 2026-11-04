<#
.SYNOPSIS
Scans local log files in the folders listed in estes.agent.md for error keywords (read-only).

.EXAMPLE
.\scan-estes-areas.ps1
Scans every area, all file ages.

.EXAMPLE
.\scan-estes-areas.ps1 -Area 'Batch','Alarms' -Hours 24
Scans areas whose name contains "Batch" or "Alarms", files modified in the last 24 hours.

.EXAMPLE
.\scan-estes-areas.ps1 -OutCsv .\Output\estes-scan.csv
Also writes the per-file results to a CSV.
#>
[CmdletBinding()]
param(
    [string[]]$Area,
    [int]$Hours = 0,
    [string]$Pattern = '\b(ERROR|FAILED|EXCEPTION|FATAL|CRITICAL|CRASH)\b',
    [string[]]$Include = @('*.log', '*.txt', '*.out', '*.err'),
    [int]$SamplesPerFile = 3,
    [string]$EstesFile = (Join-Path $PSScriptRoot '..\.github\agents\estes.agent.md'),
    [string]$OutCsv
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $EstesFile)) { throw "Estes file not found: $EstesFile" }

# Area headings look like "### 2. Batch Operations"; folders are backticked bullet paths.
$areas = [ordered]@{}
$current = $null
foreach ($line in Get-Content -LiteralPath $EstesFile) {
    if ($line -match '^###\s+\d+\.\s+(.+?)\s*$') {
        $current = $Matches[1]
        $areas[$current] = [System.Collections.Generic.List[string]]::new()
        continue
    }
    if ($current -and $line -match '^##\s') { break }
    if ($current -and $line -match '^-\s+`([A-Za-z]:\\[^`]+)`') { $areas[$current].Add($Matches[1]) }
}

$names = @($areas.Keys)
if ($Area) {
    $names = @($names | Where-Object { $n = $_; $Area | Where-Object { $n -like "*$_*" } })
}
if (-not $names) { throw "No matching areas. Available: $($areas.Keys -join ', ')" }

$since = if ($Hours -gt 0) { (Get-Date).AddHours(-$Hours) } else { $null }
$summary = [System.Collections.Generic.List[object]]::new()
$detail = [System.Collections.Generic.List[object]]::new()

foreach ($name in $names) {
    $folders = $areas[$name]
    if ($folders.Count -eq 0) {
        $summary.Add([pscustomobject]@{ Area = $name; Folder = '(none listed)'; Status = 'No folders'; Files = 0; FilesWithMatches = 0; Matches = 0 })
        continue
    }
    foreach ($folder in $folders) {
        if (-not (Test-Path -LiteralPath $folder -PathType Container)) {
            $summary.Add([pscustomobject]@{ Area = $name; Folder = $folder; Status = 'Missing'; Files = 0; FilesWithMatches = 0; Matches = 0 })
            continue
        }

        $files = @(Get-ChildItem -LiteralPath $folder -Recurse -File -Include $Include -ErrorAction SilentlyContinue |
            Where-Object { -not $since -or $_.LastWriteTime -ge $since })

        $filesWithMatches = 0
        $matchTotal = 0
        foreach ($f in $files) {
            $hits = @(Select-String -LiteralPath $f.FullName -Pattern $Pattern -ErrorAction SilentlyContinue)
            if ($hits.Count -eq 0) { continue }
            $filesWithMatches++
            $matchTotal += $hits.Count
            $samples = $hits | Select-Object -First $SamplesPerFile | ForEach-Object {
                $t = $_.Line.Trim()
                if ($t.Length -gt 120) { $t = $t.Substring(0, 120) }
                '{0}: {1}' -f $_.LineNumber, $t
            }
            $detail.Add([pscustomobject]@{
                Area      = $name
                File      = $f.FullName
                LastWrite = $f.LastWriteTime.ToString('yyyy-MM-dd HH:mm')
                Matches   = $hits.Count
                Samples   = $samples -join ' | '
            })
        }

        $status = if ($files.Count -eq 0) { 'No files' } else { 'OK' }
        $summary.Add([pscustomobject]@{ Area = $name; Folder = $folder; Status = $status; Files = $files.Count; FilesWithMatches = $filesWithMatches; Matches = $matchTotal })
    }
}

$windowText = if ($since) { "files modified since $($since.ToString('yyyy-MM-dd HH:mm'))" } else { 'all file ages' }
Write-Output "Estes file : $((Resolve-Path -LiteralPath $EstesFile).Path)"
Write-Output "Pattern    : $Pattern"
Write-Output "Window     : $windowText"
Write-Output ''
Write-Output '== Summary by folder =='
$summary | Format-Table Area, Folder, Status, Files, FilesWithMatches, Matches -AutoSize | Out-String -Width 250

Write-Output '== Files with matches (most matches first) =='
if ($detail.Count -eq 0) {
    Write-Output 'None.'
} else {
    $detail | Sort-Object Matches -Descending | Format-List Area, File, LastWrite, Matches, Samples | Out-String -Width 250
}

$missing = @($summary | Where-Object Status -eq 'Missing')
if ($missing.Count) {
    Write-Output "== Missing folders ($($missing.Count)) =="
    $missing | ForEach-Object { '  {0}  [{1}]' -f $_.Folder, $_.Area }
}

if ($OutCsv) {
    $detail | Sort-Object Matches -Descending | Export-Csv -LiteralPath $OutCsv -NoTypeInformation -Encoding UTF8
    Write-Output "CSV written: $OutCsv"
}
