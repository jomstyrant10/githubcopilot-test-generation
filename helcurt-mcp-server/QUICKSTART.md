# Helcurt Quick Start Guide

Deploy and monitor remote machines in 5 minutes - starting from VS Code!

## What You Need

- VS Code with GitHub Copilot
- Node.js 18+ (for building the executable)
- Target dev machine (IP/hostname and SSH access)
- Folders you want to monitor

## 📋 Before You Start

Have this information ready:
- 🖥️ **Target machine IP/hostname** (e.g., 192.168.1.100 or dev-server-01)
- 👤 **Username** (e.g., admin)
- 📁 **Folders to monitor** (e.g., C:\Logs\App1, C:\Logs\App2)
- 🔐 **SSH access** set up (or password ready)

**Tip:** See [CONVERSATION-TEMPLATE.md](CONVERSATION-TEMPLATE.md) for the complete template.

---

## Quick Deploy from VS Code

### Step 1: Prepare Your Information (30 seconds)

Open VS Code and prepare your deployment request:

```
=== HELCURT DEPLOYMENT ===

Goal: Deploy Helcurt to dev machine
Target: 192.168.1.100
Username: admin
Folders to monitor:
  - C:\Logs\Application
  - C:\ProgramData\MyApp\Logs
Connection: SSH
```

### Step 2: Build the Executable (2 minutes)

Open a terminal in VS Code (`` Ctrl+` ``) and run:

```powershell
cd helcurt-mcp-server
npm install
npm run build
```

This creates `helcurt-server.exe` - your deployable executable.

### Step 3: Deploy Using the Script (1 minute)

Use the automated deployment script:

```powershell
# Still in helcurt-mcp-server folder
.\deploy.ps1 `
    -TargetMachine "192.168.1.100" `
    -Username "admin" `
    -MonitoredFolders "C:\Logs\Application,C:\ProgramData\MyApp\Logs"
```

The script will:
- ✅ Connect to the remote machine via SSH
- ✅ Create the installation directory
- ✅ Copy helcurt-server.exe
- ✅ Create helcurt-config.json with your folders
- ✅ Verify the installation
- ✅ Show you the VS Code config to add

**OR Deploy Manually:**

```powershell
# Copy files to remote machine
scp helcurt-server.exe admin@192.168.1.100:C:\Tools\Helcurt\
scp helcurt-config.example.json admin@192.168.1.100:C:\Tools\Helcurt\

# SSH to remote and configure
ssh admin@192.168.1.100
cd C:\Tools\Helcurt
copy helcurt-config.example.json helcurt-config.json
notepad helcurt-config.json
# Add your folders and save
```

### Step 4: Configure VS Code (1 minute)

Add to your VS Code `settings.json` (File → Preferences → Settings → Open Settings JSON):

```json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt-dev": {
      "command": "ssh",
      "args": [
        "admin@192.168.1.100",
        "C:\\Tools\\Helcurt\\helcurt-server.exe"
      ]
    }
  }
}
```

**For local machine** (if deploying on same machine):
```json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt": {
      "command": "C:\\Tools\\Helcurt\\helcurt-server.exe",
      "args": []
    }
  }
}
```

### Step 5: Start Using Helcurt Agent! (30 seconds)

1. **Restart VS Code** to load the new MCP server
2. **Open Copilot Chat** (Ctrl+Alt+I or Cmd+Alt+I)
3. **Talk to Helcurt:**

```
@helcurt-dev I just deployed you to 192.168.1.100. 
Can you list the monitored folders?
```

```
@helcurt-dev get system health status
```

```
@helcurt-dev check for errors in the last hour
```

```
@helcurt-dev scan all logs for "exception" or "failed"
```

## 🎉 You're Done!

You can now monitor logs on your dev machine directly from VS Code using natural language!

---

## Talking to Helcurt - Example Conversations

### Initial Setup Verification

```
You: @helcurt-dev I just deployed you. Can you confirm the setup?

Helcurt: [Lists monitored folders and system info]
```

### Check for Recent Errors

```
You: @helcurt-dev scan all logs for errors from the last 2 hours

Helcurt: [Shows error matches with file paths and line numbers]
```

### Monitor Specific Application

```
You: @helcurt-dev search logs for "PaymentService" and show any errors or warnings

Helcurt: [Searches and returns relevant matches]
```

### Get System Status

```
You: @helcurt-dev show me the health status and recent critical events

Helcurt: [Provides CPU, memory, disk info and event log entries]
```

### Investigate Issues

```
You: @helcurt-dev I'm seeing timeout issues. Search for "timeout" or "connection failed" in the last 24 hours

Helcurt: [Returns matching log entries with context]
```

### Tail a Log File

```
You: @helcurt-dev show me the last 100 lines from C:\Logs\application.log

Helcurt: [Displays recent log entries]
```

---

## Using the Conversation Template

For best results, provide clear information upfront:

```
@helcurt-dev 

Goal: Check for payment processing errors
Time: Last 3 hours
Looking for: "payment failed", "transaction error", "timeout"
Priority: High severity errors only
```

See [CONVERSATION-TEMPLATE.md](CONVERSATION-TEMPLATE.md) for more templates.

---

## Deploy to Multiple Machines

### Build Once, Deploy Everywhere

You already built `helcurt-server.exe`, now deploy to more machines:

```powershell
# Deploy to test server
.\deploy.ps1 `
    -TargetMachine "192.168.1.101" `
    -Username "admin" `
    -MonitoredFolders "C:\Logs\Test"

# Deploy to staging server  
.\deploy.ps1 `
    -TargetMachine "192.168.1.102" `
    -Username "admin" `
    -MonitoredFolders "C:\Logs\Staging"
```

### Add to VS Code Settings

```json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt-dev": {
      "command": "ssh",
      "args": ["admin@192.168.1.100", "C:\\Tools\\Helcurt\\helcurt-server.exe"]
    },
    "helcurt-test": {
      "command": "ssh",
      "args": ["admin@192.168.1.101", "C:\\Tools\\Helcurt\\helcurt-server.exe"]
    },
    "helcurt-staging": {
      "command": "ssh",
      "args": ["admin@192.168.1.102", "C:\\Tools\\Helcurt\\helcurt-server.exe"]
    }
  }
}
```

### Use Each Instance

```
@helcurt-dev check for errors
@helcurt-test get health status
@helcurt-staging scan logs for "exception"
```

---

## Common Deployment Scenarios

### Scenario 1: First Time Setup

Ask Helcurt for guidance:

```
You: I want to set up monitoring on a new dev server.

Target: 192.168.1.100
Username: admin
Folders: C:\Logs\Application, C:\ProgramData\MyApp\Logs
I have SSH access configured.
What do I need to do?
```

Then follow the deployment steps above.

### Scenario 2: Add More Folders

Already deployed? Just update the config:

```powershell
# SSH to the machine
ssh admin@192.168.1.100

# Edit config
notepad C:\Tools\Helcurt\helcurt-config.json

# Add more folders and save
```

Or redeploy with updated folders:

```powershell
.\deploy.ps1 `
    -TargetMachine "192.168.1.100" `
    -Username "admin" `
    -MonitoredFolders "C:\Logs\App1,C:\Logs\App2,D:\NewLogs"
```

Then verify with Helcurt:
```
@helcurt-dev list monitored folders
```

### Scenario 3: Network Shares

Monitor centralized logs on network shares:

```json
{
  "monitoredFolders": [
    "\\\\FileServer\\Logs$\\Application",
    "\\\\FileServer\\Logs$\\Database"
  ]
}
```

Test access:
```
@helcurt-dev can you access the monitored folders?
```

---

## Adding More Folders After Deployment

Edit `helcurt-config.json` on the target machine:

```json
{
  "monitoredFolders": [
    "C:\\Logs\\App1",
    "C:\\Logs\\App2",
    "D:\\WebLogs",
    "\\\\NetworkServer\\SharedLogs"
  ]
}
```

No restart needed - just run a new query from VS Code.

---

## Troubleshooting with Helcurt

### Issue: "Server not found" or "@helcurt-dev not recognized"

**Solution:**
1. Restart VS Code after changing settings.json
2. Check the MCP server name matches in settings
3. Verify the agent appears when you type `@` in Copilot Chat

**Ask Helcurt:**
```
I added helcurt-dev to my settings but it's not showing up. What should I check?
```

### Issue: "No logs found" or Empty Results

**Solution:**
1. Verify paths in `helcurt-config.json` are correct
2. Check you have read permissions on folders
3. Ensure log files match the patterns (*.log, *.txt, etc.)

**Ask Helcurt:**
```
@helcurt-dev list monitored folders
@helcurt-dev can you access the folders?
```

### Issue: "Cannot connect" or Connection Errors

**Solution:**
- **For SSH:** Test SSH connection separately
  ```powershell
  ssh admin@192.168.1.100
  ```
- **For local:** Run executable manually to check
  ```powershell
  C:\Tools\Helcurt\helcurt-server.exe
  ```

**Ask Helcurt:**
```
@helcurt-dev I'm getting connection errors. Can you help diagnose?
```

### Issue: "Permission Denied"

**Solution:**
1. Check user account has read permissions to log folders
2. For network paths, verify network access and credentials
3. Run as administrator if needed

**Test with Helcurt:**
```
@helcurt-dev test access to C:\Logs\Application
```

### Issue: Deployment Script Fails

**Common causes:**
- SSH not enabled on target machine
- Wrong username/password
- Network connectivity issues
- Firewall blocking port 22

**Enable SSH on target:**
```powershell
# On target machine as Administrator
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
Start-Service sshd
Set-Service -Name sshd -StartupType 'Automatic'
```

---

## Quick Reference Commands

### Deployment Commands
```powershell
# Build executable
cd helcurt-mcp-server
npm install
npm run build

# Deploy to machine
.\deploy.ps1 -TargetMachine "IP" -Username "user" -MonitoredFolders "C:\Logs\App"

# Manual copy
scp helcurt-server.exe user@IP:C:\Tools\Helcurt\
```

### Helcurt Agent Commands
```
# Setup verification
@helcurt-dev list monitored folders
@helcurt-dev get system health

# Error monitoring
@helcurt-dev check for errors in the last [time]
@helcurt-dev scan logs for "[pattern]"

# Investigation
@helcurt-dev search for "[keywords]" in last [timeframe]
@helcurt-dev show me the last [N] lines from [file-path]

# Health checks
@helcurt-dev show me the health status
@helcurt-dev check disk space and memory
@helcurt-dev show Windows error events from last [time]
```

---

## Next Steps

- 📋 **Use the template:** See [CONVERSATION-TEMPLATE.md](CONVERSATION-TEMPLATE.md) for structured requests
- 🚀 **Advanced deployment:** Read [DEPLOYMENT.md](DEPLOYMENT.md) for automation and CI/CD
- ⚙️ **Configuration:** See [CONFIG.md](CONFIG.md) for all configuration options
- 📚 **Complete reference:** Check [README.md](README.md) for full documentation
- 🤖 **Deployment automation:** See [DEPLOY-AUTOMATION.md](DEPLOY-AUTOMATION.md) for scripting

---

## Pro Tips

💡 **Start conversations with context** - Provide machine name, time range, and what you're looking for upfront

💡 **Use multiple instances** - Deploy to dev, test, staging, prod and monitor each with `@helcurt-[environment]`

💡 **Leverage natural language** - Ask Helcurt questions naturally: "What errors happened during the deployment?"

💡 **Save common queries** - Create VS Code snippets for frequently used Helcurt commands

💡 **Run as Windows Service** - For 24/7 monitoring without manual starts

💡 **Use network paths** - Monitor centralized logs: `\\server\share\logs`

💡 **SSH key authentication** - Set up once, never type passwords again

💡 **Customize patterns** - Edit `helcurt-config.json` for your specific error/warning patterns

---

## Real-World Workflow Example

### Morning Standup - Check All Environments

```
@helcurt-dev get health status and errors from last 24 hours

@helcurt-test any critical issues overnight?

@helcurt-staging check for deployment errors from this morning

@helcurt-prod show me any 5xx errors in the last hour
```

### During Development - Monitor Your Changes

```
@helcurt-dev I just deployed version 2.5. Monitor for errors containing "v2.5" or "new feature"

@helcurt-dev tail the application log and show me real-time entries
```

### Incident Response - Investigate Issues

```
@helcurt-prod we got alerts at 14:30. Search for errors between 14:25 and 14:35

@helcurt-prod search for "database" AND "timeout" in that timeframe

@helcurt-prod show me all related errors with context
```

---

Happy monitoring with Helcurt! 🚀

**Need help?** Just ask `@helcurt` - it's designed to assist you!
