#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy Helcurt MCP Server to remote machines

.DESCRIPTION
    Automates deployment of helcurt-server.exe to remote Windows machines via SSH/PowerShell remoting

.PARAMETER TargetMachine
    IP address or hostname of target machine

.PARAMETER Username
    Username for remote connection

.PARAMETER Password
    Password for authentication (optional, prefer SSH keys)

.PARAMETER DeployPath
    Path on remote machine where Helcurt will be installed (default: C:\Tools\Helcurt)

.PARAMETER Method
    Deployment method: 'ssh' or 'winrm' (default: ssh)

.PARAMETER MonitoredFolders
    Comma-separated list of folders to monitor on remote machine

.EXAMPLE
    .\deploy.ps1 -TargetMachine 192.168.1.100 -Username admin -DeployPath "C:\Tools\Helcurt" -MonitoredFolders "C:\Logs\App1,C:\Logs\App2"

.EXAMPLE
    .\deploy.ps1 -TargetMachine dev-server.local -Username admin -Method winrm
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$TargetMachine,
    
    [Parameter(Mandatory=$true)]
    [string]$Username,
    
    [Parameter(Mandatory=$false)]
    [securestring]$Password,
    
    [Parameter(Mandatory=$false)]
    [string]$DeployPath = "C:\Tools\Helcurt",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet('ssh', 'winrm')]
    [string]$Method = 'ssh',
    
    [Parameter(Mandatory=$false)]
    [string]$MonitoredFolders = ""
)

# Colors for output
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Helcurt MCP Server Deployment Tool" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if helcurt-server.exe exists
if (-not (Test-Path ".\helcurt-server.exe")) {
    Write-Host "❌ Error: helcurt-server.exe not found!" -ForegroundColor Red
    Write-Host "Please build it first by running: npm run build" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Found helcurt-server.exe" -ForegroundColor Green

# Create credential object if password provided
$credential = $null
if ($Password) {
    $credential = New-Object System.Management.Automation.PSCredential($Username, $Password)
} else {
    Write-Host "ℹ️  No password provided, will use SSH key authentication" -ForegroundColor Yellow
}

# Deployment steps
Write-Host ""
Write-Host "📋 Deployment Plan:" -ForegroundColor Cyan
Write-Host "  Target: $TargetMachine"
Write-Host "  Method: $Method"
Write-Host "  Path: $DeployPath"
Write-Host "  User: $Username"
Write-Host ""

# Confirm deployment
$confirm = Read-Host "Proceed with deployment? (yes/no)"
if ($confirm -ne "yes") {
    Write-Host "Deployment cancelled." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "🚀 Starting deployment..." -ForegroundColor Green
Write-Host ""

try {
    if ($Method -eq 'ssh') {
        # SSH-based deployment
        Write-Host "Step 1: Creating remote directory..." -ForegroundColor Yellow
        
        # Convert Windows path for SSH command
        $remotePath = $DeployPath -replace '\\', '/'
        
        # Create directory
        $mkdirCmd = "powershell -Command `"New-Item -ItemType Directory -Force -Path '$DeployPath'`""
        if ($credential) {
            # SSH with password (less secure, requires sshpass)
            Write-Host "⚠️  Warning: SSH with password requires 'sshpass' tool" -ForegroundColor Yellow
            ssh "$Username@$TargetMachine" $mkdirCmd
        } else {
            # SSH with key
            ssh "$Username@$TargetMachine" $mkdirCmd
        }
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create remote directory"
        }
        Write-Host "✅ Remote directory created" -ForegroundColor Green
        
        # Copy files
        Write-Host ""
        Write-Host "Step 2: Copying files..." -ForegroundColor Yellow
        
        # Copy executable
        scp ".\helcurt-server.exe" "${Username}@${TargetMachine}:${DeployPath}/helcurt-server.exe"
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to copy helcurt-server.exe"
        }
        
        # Copy config example
        scp ".\helcurt-config.example.json" "${Username}@${TargetMachine}:${DeployPath}/helcurt-config.example.json"
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to copy config example"
        }
        
        Write-Host "✅ Files copied successfully" -ForegroundColor Green
        
        # Create config if folders specified
        if ($MonitoredFolders) {
            Write-Host ""
            Write-Host "Step 3: Creating configuration..." -ForegroundColor Yellow
            
            # Build JSON config
            $folders = $MonitoredFolders -split ',' | ForEach-Object { "`"$($_.Trim() -replace '\\', '\\')`"" }
            $foldersJson = $folders -join ','
            
            $configJson = @"
{
  `"monitoredFolders`": [$foldersJson],
  `"logPatterns`": [`"*.log`", `"*.txt`", `"*.out`", `"*.err`"],
  `"errorPatterns`": [`"ERROR`", `"CRITICAL`", `"FATAL`", `"EXCEPTION`", `"FAILED`", `"CRASH`"],
  `"warningPatterns`": [`"WARNING`", `"WARN`", `"CAUTION`", `"DEPRECATED`"],
  `"excludePatterns`": [`"node_modules/**`", `".git/**`", `"dist/**`", `"build/**`", `"*.tmp`"]
}
"@
            
            # Write config to remote machine
            $createConfigCmd = "powershell -Command `"Set-Content -Path '$DeployPath\helcurt-config.json' -Value '$($configJson -replace "'", "''")' -Encoding UTF8`""
            ssh "$Username@$TargetMachine" $createConfigCmd
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Configuration created with monitored folders" -ForegroundColor Green
            }
        } else {
            Write-Host ""
            Write-Host "Step 3: Configuration..." -ForegroundColor Yellow
            Write-Host "ℹ️  No folders specified. Please create helcurt-config.json manually." -ForegroundColor Yellow
        }
        
        # Test the installation
        Write-Host ""
        Write-Host "Step 4: Testing installation..." -ForegroundColor Yellow
        
        $testCmd = "powershell -Command `"Test-Path '$DeployPath\helcurt-server.exe'`""
        $testResult = ssh "$Username@$TargetMachine" $testCmd
        
        if ($testResult -match "True") {
            Write-Host "✅ Installation verified" -ForegroundColor Green
        } else {
            throw "Installation verification failed"
        }
        
    } elseif ($Method -eq 'winrm') {
        # PowerShell Remoting deployment
        Write-Host "Using PowerShell Remoting (WinRM)..." -ForegroundColor Yellow
        
        # Create session
        Write-Host "Connecting to remote machine..." -ForegroundColor Yellow
        $sessionParams = @{
            ComputerName = $TargetMachine
            Credential = if ($credential) { $credential } else { Get-Credential -UserName $Username -Message "Enter password for $Username" }
        }
        
        $session = New-PSSession @sessionParams
        
        if (-not $session) {
            throw "Failed to create remote session"
        }
        
        Write-Host "✅ Connected to $TargetMachine" -ForegroundColor Green
        
        # Create directory
        Write-Host ""
        Write-Host "Step 1: Creating remote directory..." -ForegroundColor Yellow
        Invoke-Command -Session $session -ScriptBlock {
            param($path)
            New-Item -ItemType Directory -Force -Path $path | Out-Null
        } -ArgumentList $DeployPath
        Write-Host "✅ Remote directory created" -ForegroundColor Green
        
        # Copy files
        Write-Host ""
        Write-Host "Step 2: Copying files..." -ForegroundColor Yellow
        Copy-Item ".\helcurt-server.exe" -ToSession $session -Destination "$DeployPath\helcurt-server.exe"
        Copy-Item ".\helcurt-config.example.json" -ToSession $session -Destination "$DeployPath\helcurt-config.example.json"
        Write-Host "✅ Files copied successfully" -ForegroundColor Green
        
        # Create config if folders specified
        if ($MonitoredFolders) {
            Write-Host ""
            Write-Host "Step 3: Creating configuration..." -ForegroundColor Yellow
            
            $folders = $MonitoredFolders -split ',' | ForEach-Object { $_.Trim() }
            
            Invoke-Command -Session $session -ScriptBlock {
                param($path, $folders)
                
                $config = @{
                    monitoredFolders = $folders
                    logPatterns = @("*.log", "*.txt", "*.out", "*.err")
                    errorPatterns = @("ERROR", "CRITICAL", "FATAL", "EXCEPTION", "FAILED", "CRASH")
                    warningPatterns = @("WARNING", "WARN", "CAUTION", "DEPRECATED")
                    excludePatterns = @("node_modules/**", ".git/**", "dist/**", "build/**", "*.tmp")
                }
                
                $config | ConvertTo-Json | Set-Content -Path "$path\helcurt-config.json" -Encoding UTF8
            } -ArgumentList $DeployPath, $folders
            
            Write-Host "✅ Configuration created with monitored folders" -ForegroundColor Green
        }
        
        # Test installation
        Write-Host ""
        Write-Host "Step 4: Testing installation..." -ForegroundColor Yellow
        $testResult = Invoke-Command -Session $session -ScriptBlock {
            param($path)
            Test-Path "$path\helcurt-server.exe"
        } -ArgumentList $DeployPath
        
        if ($testResult) {
            Write-Host "✅ Installation verified" -ForegroundColor Green
        }
        
        # Clean up
        Remove-PSSession -Session $session
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "✨ Deployment Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "📝 Next Steps:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Add to VS Code settings.json:" -ForegroundColor White
    Write-Host @"
   {
     "github.copilot.chat.mcp.servers": {
       "helcurt-$TargetMachine": {
         "command": "ssh",
         "args": ["$Username@$TargetMachine", "$DeployPath\\helcurt-server.exe"]
       }
     }
   }
"@ -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Restart VS Code" -ForegroundColor White
    Write-Host ""
    Write-Host "3. Test with:" -ForegroundColor White
    Write-Host "   @helcurt-$TargetMachine get health status" -ForegroundColor Gray
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "❌ Deployment failed: $_" -ForegroundColor Red
    Write-Host ""
    exit 1
}
