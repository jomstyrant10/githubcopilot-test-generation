# Helcurt Deployment Guide

Complete guide for deploying the Helcurt MCP server to target machines.

## Overview

Helcurt consists of:
1. **Agent Definition** (`.github/agents/helcurt.agent.md`) - Lives in your VS Code workspace
2. **MCP Server Executable** (`helcurt-server.exe`) - Deployed to target machines
3. **Configuration File** (`helcurt-config.json`) - Deployed with the executable

## Step-by-Step Deployment

### Part 1: Build the Executable

On your development machine:

1. **Navigate to the helcurt-mcp-server folder:**
   ```powershell
   cd helcurt-mcp-server
   ```

2. **Install dependencies:**
   ```powershell
   npm install
   ```

3. **Build the executable:**
   ```powershell
   npm run build
   ```
   
   This creates `helcurt-server.exe` in the current directory.

### Part 2: Deploy to Target Machine

1. **Copy files to target machine:**
   - Copy `helcurt-server.exe` to target (e.g., `C:\Tools\Helcurt\`)
   - Copy `helcurt-config.example.json` to target

2. **On the target machine, create configuration:**
   ```powershell
   cd C:\Tools\Helcurt
   copy helcurt-config.example.json helcurt-config.json
   notepad helcurt-config.json
   ```

3. **Edit `helcurt-config.json` to add your folders:**
   ```json
   {
     "monitoredFolders": [
       "C:\\Logs\\Application",
       "C:\\ProgramData\\YourApp\\Logs",
       "D:\\Data\\Logs"
     ]
   }
   ```

4. **Test the server:**
   ```powershell
   .\helcurt-server.exe
   ```
   
   You should see: `Helcurt MCP Server running...`

### Part 3: Configure VS Code

#### For Local Machine

If the target machine is your local machine, add to VS Code `settings.json`:

```json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt": {
      "command": "C:\\Tools\\Helcurt\\helcurt-server.exe",
      "args": [],
      "env": {}
    }
  }
}
```

#### For Remote Machine via SSH

If accessing a remote Windows machine via SSH:

```json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt-prod": {
      "command": "ssh",
      "args": [
        "admin@production-server",
        "C:\\Tools\\Helcurt\\helcurt-server.exe"
      ]
    }
  }
}
```

#### For Remote Machine via WinRM/PowerShell Remoting

```json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt-remote": {
      "command": "powershell",
      "args": [
        "-Command",
        "Invoke-Command -ComputerName remote-machine -ScriptBlock { C:\\Tools\\Helcurt\\helcurt-server.exe }"
      ]
    }
  }
}
```

### Part 4: Verify Setup

1. **Restart VS Code** to load the new MCP server configuration

2. **Open VS Code Chat** (Ctrl+Alt+I)

3. **Test Helcurt:**
   ```
   @helcurt list the monitored folders
   ```

4. **Check health:**
   ```
   @helcurt get system health status
   ```

## Folder Configuration

### Where to Specify Folders

**The `helcurt-config.json` file on the target machine** is where you specify all folders to monitor. This file must be in the same directory as `helcurt-server.exe`.

### Adding Folders

Edit `helcurt-config.json` on the target machine:

```json
{
  "monitoredFolders": [
    "C:\\Logs\\Application",
    "C:\\ProgramData\\MyApp\\Logs",
    "D:\\Websites\\WebApp1\\Logs",
    "\\\\FileServer\\Logs$\\Production"
  ]
}
```

**Key Points:**
- Use absolute paths
- Use double backslashes (`\\`) for Windows paths in JSON
- Can include network UNC paths (`\\server\share`)
- Requires read permissions to all folders
- Changes require server restart

### Example Configurations

#### IIS Logs
```json
{
  "monitoredFolders": [
    "C:\\inetpub\\logs\\LogFiles"
  ],
  "logPatterns": ["*.log"],
  "errorPatterns": ["500", "503", "ERROR", "Exception"]
}
```

#### Application Logs
```json
{
  "monitoredFolders": [
    "C:\\ProgramData\\YourApp\\Logs",
    "C:\\Logs\\Application"
  ],
  "logPatterns": ["*.log", "*.txt"],
  "errorPatterns": ["ERROR", "FATAL", "EXCEPTION"]
}
```

#### Multiple Servers (via multiple MCP instances)
```json
// In VS Code settings.json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt-web": {
      "command": "ssh",
      "args": ["admin@webserver", "C:\\Tools\\Helcurt\\helcurt-server.exe"]
    },
    "helcurt-db": {
      "command": "ssh",
      "args": ["admin@dbserver", "C:\\Tools\\Helcurt\\helcurt-server.exe"]
    }
  }
}
```

## Advanced Deployment Scenarios

### Running as a Windows Service

To keep Helcurt running continuously:

1. **Install NSSM (Non-Sucking Service Manager):**
   ```powershell
   choco install nssm
   ```

2. **Create service:**
   ```powershell
   nssm install HelcurtMCP "C:\Tools\Helcurt\helcurt-server.exe"
   nssm set HelcurtMCP AppDirectory "C:\Tools\Helcurt"
   nssm start HelcurtMCP
   ```

### Using with Docker

Create a `Dockerfile`:

```dockerfile
FROM node:18-alpine
WORKDIR /app
COPY helcurt-server.exe .
COPY helcurt-config.json .
CMD ["./helcurt-server.exe"]
```

### Automated Deployment Script

Create `deploy.ps1`:

```powershell
param(
    [string]$TargetMachine,
    [string]$TargetPath = "C:\Tools\Helcurt"
)

# Copy files
Copy-Item "helcurt-server.exe" "\\$TargetMachine\$($TargetPath -replace ':', '$')"
Copy-Item "helcurt-config.example.json" "\\$TargetMachine\$($TargetPath -replace ':', '$')"

# Test connection
Invoke-Command -ComputerName $TargetMachine -ScriptBlock {
    param($path)
    & "$path\helcurt-server.exe" --version
} -ArgumentList $TargetPath
```

## Monitoring Multiple Machines

To monitor multiple machines, create multiple MCP server entries:

```json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt-prod-web1": {
      "command": "ssh",
      "args": ["admin@prod-web1", "C:\\Tools\\Helcurt\\helcurt-server.exe"]
    },
    "helcurt-prod-web2": {
      "command": "ssh",
      "args": ["admin@prod-web2", "C:\\Tools\\Helcurt\\helcurt-server.exe"]
    },
    "helcurt-prod-api": {
      "command": "ssh",
      "args": ["admin@prod-api", "C:\\Tools\\Helcurt\\helcurt-server.exe"]
    }
  }
}
```

Then use in VS Code:
```
@helcurt-prod-web1 check for errors
@helcurt-prod-api get health status
```

## Security Best Practices

1. **Least Privilege**: Run Helcurt with minimal required permissions
2. **Secure SSH**: Use key-based authentication for remote access
3. **Firewall**: No inbound ports needed (uses stdio/SSH)
4. **Audit**: Monitor who accesses Helcurt logs
5. **Encryption**: Use SSH for remote connections

## Troubleshooting

### Server won't start
- Verify `helcurt-config.json` exists and is valid JSON
- Check file permissions on executable
- Try running from command prompt to see errors

### Cannot connect from VS Code
- Restart VS Code after changing settings
- Check MCP server name matches in settings
- Verify command path is correct
- For SSH: test SSH connection separately

### No logs found
- Verify folders exist and are accessible
- Check `logPatterns` in config
- Ensure user has read permissions
- Test by navigating to folder in Explorer

### Permission Denied
- Run as administrator (if needed)
- Check folder ACLs
- For network paths, verify credentials

## Maintenance

### Updating Helcurt

1. Build new version
2. Stop old server (if running as service)
3. Replace `helcurt-server.exe`
4. Restart server
5. Test functionality

### Backup Configuration

```powershell
# Backup config
Copy-Item "C:\Tools\Helcurt\helcurt-config.json" "C:\Backups\helcurt-config-$(Get-Date -Format 'yyyyMMdd').json"
```

## Next Steps

1. ✅ Deploy Helcurt to your first target machine
2. ✅ Configure monitored folders
3. ✅ Add to VS Code settings
4. ✅ Test with `@helcurt get health`
5. ✅ Add more machines as needed

## Support

- **Configuration**: See [CONFIG.md](CONFIG.md)
- **Usage**: See [README.md](README.md)
- **Agent**: See `.github/agents/helcurt.agent.md`
