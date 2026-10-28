# Helcurt Deployment Automation

Automated deployment tools for Helcurt MCP Server to remote machines.

## Quick Deploy with Script

### Using PowerShell Script

```powershell
# Deploy with SSH (recommended)
.\deploy.ps1 `
    -TargetMachine "192.168.1.100" `
    -Username "admin" `
    -MonitoredFolders "C:\Logs\App1,C:\Logs\App2"

# Deploy with PowerShell Remoting (WinRM)
.\deploy.ps1 `
    -TargetMachine "dev-server.local" `
    -Username "admin" `
    -Method winrm `
    -DeployPath "C:\Tools\Helcurt" `
    -MonitoredFolders "C:\Logs\Production,D:\AppLogs"
```

### Parameters

| Parameter | Required | Description | Default |
|-----------|----------|-------------|---------|
| `TargetMachine` | Yes | IP address or hostname | - |
| `Username` | Yes | Remote machine username | - |
| `Password` | No | Secure password (prompts if not provided) | - |
| `DeployPath` | No | Installation path on remote machine | `C:\Tools\Helcurt` |
| `Method` | No | Deployment method: `ssh` or `winrm` | `ssh` |
| `MonitoredFolders` | No | Comma-separated list of folders to monitor | - |

## What the Script Does

1. ✅ **Validates** - Checks helcurt-server.exe exists locally
2. ✅ **Connects** - Establishes SSH or WinRM connection
3. ✅ **Creates Directory** - Makes the installation folder on remote machine
4. ✅ **Copies Files** - Transfers executable and config files
5. ✅ **Configures** - Creates helcurt-config.json with your folders
6. ✅ **Verifies** - Tests the installation
7. ✅ **Provides Instructions** - Shows how to add to VS Code settings

## Prerequisites

### For SSH Deployment

On your laptop:
- OpenSSH client installed (included in Windows 10+)
- `scp` command available

On target machine:
- OpenSSH server running
- Firewall allows port 22

**Enable SSH on target:**
```powershell
# On remote machine (as Administrator)
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
Start-Service sshd
Set-Service -Name sshd -StartupType 'Automatic'
```

### For WinRM Deployment

On target machine:
- PowerShell Remoting enabled
- Firewall allows port 5985 (HTTP) or 5986 (HTTPS)

**Enable WinRM on target:**
```powershell
# On remote machine (as Administrator)
Enable-PSRemoting -Force
Set-Item WSMan:\localhost\Client\TrustedHosts -Value "*" -Force
```

## Usage Examples

### Deploy to Single Machine

```powershell
# Basic deployment
.\deploy.ps1 -TargetMachine "192.168.1.100" -Username "admin"

# With specific folders
.\deploy.ps1 `
    -TargetMachine "192.168.1.100" `
    -Username "admin" `
    -MonitoredFolders "C:\Logs\WebApp,C:\Logs\Database,D:\AppLogs"

# Custom installation path
.\deploy.ps1 `
    -TargetMachine "192.168.1.100" `
    -Username "admin" `
    -DeployPath "D:\Tools\Helcurt"
```

### Deploy to Multiple Machines

Create a simple loop script:

```powershell
# deploy-multiple.ps1
$machines = @(
    @{ IP = "192.168.1.100"; Name = "web-server-1"; Folders = "C:\inetpub\logs" },
    @{ IP = "192.168.1.101"; Name = "web-server-2"; Folders = "C:\inetpub\logs" },
    @{ IP = "192.168.1.200"; Name = "db-server"; Folders = "C:\Logs\Database" }
)

foreach ($machine in $machines) {
    Write-Host "Deploying to $($machine.Name)..." -ForegroundColor Cyan
    
    .\deploy.ps1 `
        -TargetMachine $machine.IP `
        -Username "admin" `
        -MonitoredFolders $machine.Folders `
        -DeployPath "C:\Tools\Helcurt"
    
    Write-Host "Completed $($machine.Name)" -ForegroundColor Green
    Write-Host ""
}
```

## Security Best Practices

### Use SSH Keys (No Password Prompts)

Generate SSH key pair:
```powershell
# On your laptop
ssh-keygen -t ed25519 -C "your_email@example.com"
```

Copy public key to remote machine:
```powershell
# Copy the key
type $env:USERPROFILE\.ssh\id_ed25519.pub | `
    ssh admin@192.168.1.100 "cat >> .ssh/authorized_keys"
```

Now deploy without password:
```powershell
.\deploy.ps1 -TargetMachine "192.168.1.100" -Username "admin"
# No password prompt!
```

### Secure Credential Storage

Use Windows Credential Manager:
```powershell
# Store credentials
cmdkey /add:192.168.1.100 /user:admin /pass:YourPassword

# Deploy will use stored credentials
.\deploy.ps1 -TargetMachine "192.168.1.100" -Username "admin"
```

## After Deployment

### 1. Add to VS Code Settings

The script will show you the exact config. Add to `settings.json`:

```json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt-server1": {
      "command": "ssh",
      "args": [
        "admin@192.168.1.100",
        "C:\\Tools\\Helcurt\\helcurt-server.exe"
      ]
    }
  }
}
```

### 2. Test the Connection

```
@helcurt-server1 list monitored folders
@helcurt-server1 get health status
```

## Troubleshooting

### "helcurt-server.exe not found"

Build it first:
```powershell
npm install
npm run build
```

### "SSH connection failed"

- Verify SSH is running: `ssh admin@192.168.1.100`
- Check firewall allows port 22
- Verify username/password

### "Access Denied" or "Permission Denied"

- Check user has admin rights on remote machine
- Verify username is correct
- Use SSH key authentication

### "WinRM connection failed"

Enable on target machine:
```powershell
Enable-PSRemoting -Force
```

### Files not copying

- Check network connectivity
- Verify remote path is accessible
- Check disk space on remote machine

## Manual Deployment

If the script doesn't work, deploy manually:

```powershell
# Copy files
scp helcurt-server.exe admin@192.168.1.100:C:\Tools\Helcurt\
scp helcurt-config.example.json admin@192.168.1.100:C:\Tools\Helcurt\

# Create config
ssh admin@192.168.1.100 "copy C:\Tools\Helcurt\helcurt-config.example.json C:\Tools\Helcurt\helcurt-config.json"

# Edit config
ssh admin@192.168.1.100 "notepad C:\Tools\Helcurt\helcurt-config.json"
```

## Advanced: Deploy via Ansible

Create `deploy-helcurt.yml`:

```yaml
---
- name: Deploy Helcurt MCP Server
  hosts: all
  vars:
    install_path: "C:\\Tools\\Helcurt"
    monitored_folders:
      - "C:\\Logs\\Application"
      - "C:\\ProgramData\\MyApp\\Logs"
  
  tasks:
    - name: Create installation directory
      win_file:
        path: "{{ install_path }}"
        state: directory
    
    - name: Copy Helcurt executable
      win_copy:
        src: ./helcurt-server.exe
        dest: "{{ install_path }}\\helcurt-server.exe"
    
    - name: Create configuration
      win_copy:
        content: |
          {
            "monitoredFolders": {{ monitored_folders | to_json }},
            "logPatterns": ["*.log", "*.txt"],
            "errorPatterns": ["ERROR", "CRITICAL"],
            "warningPatterns": ["WARNING", "WARN"]
          }
        dest: "{{ install_path }}\\helcurt-config.json"
```

Run with:
```bash
ansible-playbook -i inventory.ini deploy-helcurt.yml
```

## CI/CD Integration

### Azure DevOps Pipeline

```yaml
steps:
- task: PowerShell@2
  displayName: 'Deploy Helcurt to Dev'
  inputs:
    filePath: 'helcurt-mcp-server/deploy.ps1'
    arguments: >
      -TargetMachine $(DevServerIP)
      -Username $(DevServerUser)
      -Password (ConvertTo-SecureString $(DevServerPassword) -AsPlainText -Force)
      -MonitoredFolders "C:\Logs\App"
```

### GitHub Actions

```yaml
- name: Deploy Helcurt
  run: |
    ./helcurt-mcp-server/deploy.ps1 `
      -TargetMachine ${{ secrets.TARGET_IP }} `
      -Username ${{ secrets.TARGET_USER }} `
      -MonitoredFolders "C:\Logs\Production"
  shell: pwsh
```

## Next Steps

1. ✅ Build helcurt-server.exe
2. ✅ Run deployment script
3. ✅ Add to VS Code settings
4. ✅ Test with `@helcurt-servername`
5. ✅ Deploy to more machines as needed

## Support

- For script issues: Check error messages and prerequisites
- For SSH setup: See [DEPLOYMENT.md](DEPLOYMENT.md)
- For config help: See [CONFIG.md](CONFIG.md)
