# Bug Report Log Extraction Tool
# PowerShell version for Windows users
# Extracts relevant log files based on bug type for easier bug reporting.

[CmdletBinding()]
param(
    [Parameter(Position=0)]
    [string]$BugType,
    
    [Parameter()]
    [switch]$List,
    
    [Parameter()]
    [switch]$NoCompress,
    
    [Parameter()]
    [string]$Config = "bug-report-config.json"
)

# Set strict mode for better error handling
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

class BugLogExtractor {
    [hashtable]$Config
    [string]$ScriptDir
    [string]$WorkspaceRoot
    
    BugLogExtractor([string]$configPath) {
        $this.ScriptDir = Split-Path -Parent $PSCommandPath
        $this.WorkspaceRoot = Split-Path -Parent $this.ScriptDir
        $this.Config = $this.LoadConfig($configPath)
    }
    
    [hashtable] LoadConfig([string]$configPath) {
        $fullPath = Join-Path $this.ScriptDir $configPath
        
        if (-not (Test-Path $fullPath)) {
            Write-Host "❌ Configuration file not found: $fullPath" -ForegroundColor Red
            exit 1
        }
        
        try {
            $json = Get-Content $fullPath -Raw | ConvertFrom-Json
            return @{
                bugTypes = $json.bugTypes
                globalSettings = $json.globalSettings
            }
        }
        catch {
            Write-Host "❌ Invalid JSON in configuration file: $_" -ForegroundColor Red
            exit 1
        }
    }
    
    [System.Collections.Generic.List[string]] ExpandEnvPath([string]$path) {
        # Expand environment variables
        $expanded = [Environment]::ExpandEnvironmentVariables($path)
        
        # Convert to absolute path if relative
        if (-not [System.IO.Path]::IsPathRooted($expanded)) {
            $expanded = Join-Path $this.WorkspaceRoot $expanded
        }
        
        # Handle wildcards
        $results = New-Object System.Collections.Generic.List[string]
        
        if ($expanded -match '\*') {
            $matches = Get-ChildItem -Path $expanded -ErrorAction SilentlyContinue -Recurse -File
            foreach ($match in $matches) {
                $results.Add($match.FullName)
            }
        }
        else {
            $results.Add($expanded)
        }
        
        return $results
    }
    
    [double] GetFileSizeMB([string]$filePath) {
        if (Test-Path $filePath) {
            return (Get-Item $filePath).Length / 1MB
        }
        return 0.0
    }
    
    [bool] MatchesPattern([string]$filePath, [array]$patterns) {
        foreach ($pattern in $patterns) {
            if ($filePath -like $pattern) {
                return $true
            }
        }
        return $false
    }
    
    [System.Collections.Generic.List[string]] CollectFiles([PSCustomObject]$bugTypeConfig) {
        $collected = New-Object System.Collections.Generic.List[string]
        $maxSizeMB = if ($bugTypeConfig.maxFileSizeMB) { $bugTypeConfig.maxFileSizeMB } else { 10 }
        $includePatterns = if ($bugTypeConfig.includePatterns) { $bugTypeConfig.includePatterns } else { @('*') }
        $logPaths = if ($bugTypeConfig.logPaths) { $bugTypeConfig.logPaths } else { @() }
        
        foreach ($logPath in $logPaths) {
            $expandedPaths = $this.ExpandEnvPath($logPath)
            
            foreach ($path in $expandedPaths) {
                if (Test-Path $path -PathType Leaf) {
                    # Single file
                    if ($this.MatchesPattern($path, $includePatterns)) {
                        $sizeMB = $this.GetFileSizeMB($path)
                        if ($sizeMB -le $maxSizeMB) {
                            $collected.Add($path)
                        }
                        else {
                            Write-Host "⚠️  Skipping large file: $path ($([math]::Round($sizeMB, 2)) MB)" -ForegroundColor Yellow
                        }
                    }
                }
                elseif (Test-Path $path -PathType Container) {
                    # Directory
                    foreach ($pattern in $includePatterns) {
                        $files = Get-ChildItem -Path $path -Filter $pattern -Recurse -File -ErrorAction SilentlyContinue
                        foreach ($file in $files) {
                            $sizeMB = $this.GetFileSizeMB($file.FullName)
                            if ($sizeMB -le $maxSizeMB) {
                                $collected.Add($file.FullName)
                            }
                            else {
                                Write-Host "⚠️  Skipping large file: $($file.FullName) ($([math]::Round($sizeMB, 2)) MB)" -ForegroundColor Yellow
                            }
                        }
                    }
                }
            }
        }
        
        # Remove duplicates
        return ($collected | Select-Object -Unique)
    }
    
    [string] CreateOutputDirectory([string]$bugType) {
        $globalSettings = $this.Config.globalSettings
        $outputBase = Join-Path $this.WorkspaceRoot $globalSettings.outputDirectory
        
        if ($globalSettings.createTimestampedFolder) {
            $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
            $outputDir = Join-Path $outputBase "${bugType}_${timestamp}"
        }
        else {
            $outputDir = Join-Path $outputBase $bugType
        }
        
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        return $outputDir
    }
    
    [void] CreateSystemInfoFile([string]$outputDir) {
        $systemInfo = @{
            timestamp = (Get-Date).ToString("o")
            platform = [System.Environment]::OSVersion.VersionString
            powershell_version = $PSVersionTable.PSVersion.ToString()
            machine = [System.Environment]::MachineName
            processor = (Get-WmiObject -Class Win32_Processor).Name
            workspace_root = $this.WorkspaceRoot
        }
        
        $infoFile = Join-Path $outputDir "system_info.json"
        $systemInfo | ConvertTo-Json | Set-Content $infoFile -Encoding UTF8
        
        Write-Host "✅ System info saved to: $(Split-Path -Leaf $infoFile)" -ForegroundColor Green
    }
    
    [void] CopyFiles([System.Collections.Generic.List[string]]$files, [string]$outputDir) {
        if ($files.Count -eq 0) {
            Write-Host "⚠️  No files found to copy." -ForegroundColor Yellow
            return
        }
        
        $logsDir = Join-Path $outputDir "logs"
        New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
        
        $copiedCount = 0
        foreach ($filePath in $files) {
            try {
                $fileName = Split-Path -Leaf $filePath
                $dest = Join-Path $logsDir $fileName
                
                # Handle name conflicts
                $counter = 1
                $originalDest = $dest
                while (Test-Path $dest) {
                    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($originalDest)
                    $extension = [System.IO.Path]::GetExtension($originalDest)
                    $dest = Join-Path (Split-Path $originalDest) "${baseName}_${counter}${extension}"
                    $counter++
                }
                
                Copy-Item -Path $filePath -Destination $dest -Force
                $copiedCount++
                Write-Host "📄 Copied: $fileName" -ForegroundColor Cyan
            }
            catch {
                Write-Host "❌ Failed to copy $filePath: $_" -ForegroundColor Red
            }
        }
        
        Write-Host "`n✅ Successfully copied $copiedCount file(s)" -ForegroundColor Green
    }
    
    [string] CompressOutput([string]$outputDir, [string]$bugType) {
        $zipName = "$(Split-Path -Leaf $outputDir).zip"
        $zipPath = Join-Path (Split-Path -Parent $outputDir) $zipName
        
        # Remove existing zip if present
        if (Test-Path $zipPath) {
            Remove-Item $zipPath -Force
        }
        
        Compress-Archive -Path "$outputDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
        
        # Remove the uncompressed directory
        Remove-Item -Path $outputDir -Recurse -Force
        
        return $zipPath
    }
    
    [void] ExtractLogs([string]$bugType, [bool]$compress) {
        $bugTypes = $this.Config.bugTypes
        
        if (-not $bugTypes.$bugType) {
            Write-Host "❌ Unknown bug type: $bugType" -ForegroundColor Red
            Write-Host "Available types: $($bugTypes.PSObject.Properties.Name -join ', ')" -ForegroundColor Yellow
            exit 1
        }
        
        $bugConfig = $bugTypes.$bugType
        Write-Host "`n🔍 Extracting logs for: $bugType" -ForegroundColor Cyan
        Write-Host "📝 Description: $($bugConfig.description)`n" -ForegroundColor Gray
        
        # Collect files
        Write-Host "📂 Collecting files..." -ForegroundColor Cyan
        $files = $this.CollectFiles($bugConfig)
        Write-Host "Found $($files.Count) file(s)`n" -ForegroundColor Cyan
        
        # Create output directory
        $outputDir = $this.CreateOutputDirectory($bugConfig.outputPrefix ?? $bugType)
        Write-Host "📁 Output directory: $outputDir`n" -ForegroundColor Cyan
        
        # Copy files
        $this.CopyFiles($files, $outputDir)
        
        # Add system info if enabled
        if ($this.Config.globalSettings.includeSystemInfo) {
            $this.CreateSystemInfoFile($outputDir)
        }
        
        # Compress if enabled
        if ($compress) {
            Write-Host "`n📦 Compressing output..." -ForegroundColor Cyan
            $zipPath = $this.CompressOutput($outputDir, $bugType)
            Write-Host "✅ Bug report package created: $zipPath`n" -ForegroundColor Green
            Write-Host "📤 You can now attach this file to your bug report." -ForegroundColor Green
        }
        else {
            Write-Host "`n✅ Bug report files extracted to: $outputDir`n" -ForegroundColor Green
        }
    }
    
    [void] ListBugTypes() {
        $bugTypes = $this.Config.bugTypes
        
        Write-Host "`n📋 Available Bug Types:`n" -ForegroundColor Cyan
        foreach ($property in $bugTypes.PSObject.Properties) {
            $bugType = $property.Name
            $description = $property.Value.description ?? "No description"
            Write-Host "  • $bugType" -ForegroundColor White
            Write-Host "    $description`n" -ForegroundColor Gray
        }
    }
}

# Main execution
try {
    $extractor = [BugLogExtractor]::new($Config)
    
    if ($List) {
        $extractor.ListBugTypes()
    }
    elseif ($BugType) {
        $shouldCompress = -not $NoCompress
        $extractor.ExtractLogs($BugType, $shouldCompress)
    }
    else {
        Write-Host @"

Bug Report Log Extraction Tool
Usage: .\extract-bug-logs.ps1 <bug_type> [-NoCompress] [-List]

Examples:
  # Extract logs for test generation failure
  .\extract-bug-logs.ps1 test-generation-failure
  
  # Extract logs without compression
  .\extract-bug-logs.ps1 general -NoCompress
  
  # List all available bug types
  .\extract-bug-logs.ps1 -List

"@
        $extractor.ListBugTypes()
    }
}
catch {
    Write-Host "`n❌ Error: $_" -ForegroundColor Red
    exit 1
}
