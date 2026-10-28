# Helcurt Deployment Checklist

Use this checklist to ensure proper deployment and configuration.

## ☐ Phase 1: Build the Executable

### On Development Machine

- [ ] Node.js 18+ is installed
- [ ] Navigated to `helcurt-mcp-server` folder
- [ ] Ran `npm install`
- [ ] Ran `npm run build` (or `build.bat` / `build.ps1`)
- [ ] Verified `helcurt-server.exe` was created
- [ ] Verified no build errors

## ☐ Phase 2: Deploy to Target Machine

### Files to Copy

- [ ] Copied `helcurt-server.exe` to target machine
- [ ] Copied `helcurt-config.example.json` to target machine
- [ ] Files are in same directory (e.g., `C:\Tools\Helcurt\`)

### Verify Deployment

- [ ] Navigated to deployment folder on target machine
- [ ] Files are present and readable
- [ ] User account has execute permission on `.exe`

## ☐ Phase 3: Configure Folders

### On Target Machine

- [ ] Renamed `helcurt-config.example.json` to `helcurt-config.json`
- [ ] Opened `helcurt-config.json` in text editor
- [ ] Added all folders you want to monitor:
  ```json
  {
    "monitoredFolders": [
      "C:\\Logs\\YourApp",
      "C:\\Other\\Path"
    ]
  }
  ```
- [ ] Used double backslashes (`\\`) for Windows paths
- [ ] Verified JSON syntax is valid (no trailing commas, quotes matched)
- [ ] Saved the file

### Verify Folder Access

- [ ] All listed folders exist
- [ ] User account has read permissions to all folders
- [ ] For network paths: verified network connectivity
- [ ] For network paths: verified credentials/permissions

## ☐ Phase 4: Test the Server Locally

### On Target Machine

- [ ] Opened Command Prompt or PowerShell
- [ ] Navigated to Helcurt folder
- [ ] Ran `.\helcurt-server.exe`
- [ ] Saw "Helcurt MCP Server running..." message
- [ ] Saw "Monitored folders:" followed by your folders
- [ ] No errors displayed
- [ ] Pressed Ctrl+C to stop

### If Errors Occurred

- [ ] Checked `helcurt-config.json` syntax
- [ ] Verified all folder paths are correct
- [ ] Checked file permissions
- [ ] Reviewed error message and fixed issue

## ☐ Phase 5: Configure VS Code

### Open VS Code Settings

- [ ] Opened VS Code
- [ ] Went to File → Preferences → Settings
- [ ] Clicked "Open Settings (JSON)" icon (top right)

### Add MCP Server Entry

**For Local Machine:**
- [ ] Added this to `settings.json`:
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
- [ ] Replaced path with your actual path
- [ ] Verified JSON syntax is correct
- [ ] Saved settings

**For Remote Machine via SSH:**
- [ ] Added this to `settings.json`:
  ```json
  {
    "github.copilot.chat.mcp.servers": {
      "helcurt": {
        "command": "ssh",
        "args": [
          "user@remote-machine",
          "C:\\Tools\\Helcurt\\helcurt-server.exe"
        ]
      }
    }
  }
  ```
- [ ] Replaced `user@remote-machine` with actual SSH connection
- [ ] Replaced path with actual remote path
- [ ] Verified SSH works independently: `ssh user@remote-machine`
- [ ] Saved settings

### Restart VS Code

- [ ] Closed all VS Code windows
- [ ] Reopened VS Code
- [ ] Settings reloaded

## ☐ Phase 6: Verify Agent is Loaded

### Check Agent Availability

- [ ] Opened Copilot Chat (Ctrl+Alt+I or Cmd+Alt+I)
- [ ] Typed `@` and looked for "helcurt" in list
- [ ] Verified agent appears

### If Agent Not Showing

- [ ] Checked `.github/agents/helcurt.agent.md` exists in workspace
- [ ] Verified file has correct frontmatter
- [ ] Reloaded VS Code window
- [ ] Checked VS Code output/console for errors

## ☐ Phase 7: Test Agent Functionality

### Basic Tests

- [ ] **Test 1 - List folders:**
  ```
  @helcurt list the monitored folders
  ```
  - [ ] Command executed successfully
  - [ ] Saw list of configured folders
  - [ ] Folders match `helcurt-config.json`

- [ ] **Test 2 - Get health:**
  ```
  @helcurt get system health status
  ```
  - [ ] Command executed successfully
  - [ ] Saw system information (CPU, memory, etc.)
  - [ ] No errors

- [ ] **Test 3 - Scan logs:**
  ```
  @helcurt scan for errors in the last hour
  ```
  - [ ] Command executed successfully
  - [ ] Saw scan results (even if no errors found)
  - [ ] No connection/permission errors

### Advanced Tests

- [ ] **Test 4 - Search logs:**
  ```
  @helcurt search logs for "your-search-term"
  ```
  - [ ] Search executed successfully
  - [ ] Results match expected logs

- [ ] **Test 5 - Tail a log:**
  ```
  @helcurt show last 50 lines from C:\path\to\your.log
  ```
  - [ ] File contents displayed
  - [ ] Correct number of lines shown

- [ ] **Test 6 - Windows Events (if Windows):**
  ```
  @helcurt show Windows error events from last 30 minutes
  ```
  - [ ] Events retrieved successfully
  - [ ] Or appropriate error if not Windows/no permissions

## ☐ Phase 8: Production Readiness (Optional)

### For Continuous Monitoring

- [ ] Installed NSSM or similar service manager
- [ ] Configured Helcurt as Windows Service
- [ ] Set service to auto-start
- [ ] Verified service starts on reboot

### For Multiple Machines

- [ ] Deployed to all target machines
- [ ] Each has unique `helcurt-config.json`
- [ ] Added all machines to VS Code settings
- [ ] Tested each with unique agent name:
  - [ ] `@helcurt-prod`
  - [ ] `@helcurt-staging`
  - [ ] `@helcurt-dev`

### Security Review

- [ ] Verified least-privilege access
- [ ] Documented which accounts run Helcurt
- [ ] Secured `helcurt-config.json` permissions
- [ ] For SSH: using key-based authentication
- [ ] Reviewed what folders are accessible

### Documentation

- [ ] Documented deployment locations
- [ ] Documented monitored folders per machine
- [ ] Documented which team members have access
- [ ] Created runbook for common issues
- [ ] Shared usage examples with team

## ☐ Troubleshooting Checklist

### If Not Working

- [ ] Checked VS Code output for errors
- [ ] Verified executable path in settings.json
- [ ] Confirmed `helcurt-config.json` is valid JSON
- [ ] Tested server manually on target machine
- [ ] Verified all folder paths exist and are accessible
- [ ] Restarted VS Code
- [ ] Checked firewall/antivirus not blocking
- [ ] For SSH: tested SSH connection separately

### Common Issues Fixed

- [ ] Added double backslashes in JSON paths
- [ ] Fixed trailing commas in JSON
- [ ] Corrected folder permissions
- [ ] Updated VS Code settings path
- [ ] Restarted VS Code after config changes

## ✅ Deployment Complete!

Once all checkboxes are checked, your Helcurt deployment is complete and ready for use!

### Quick Reference

**To add more folders:**
Edit `helcurt-config.json` on target machine and restart query.

**To monitor additional machines:**
Deploy Helcurt to new machine and add to VS Code settings.

**To update Helcurt:**
Rebuild executable and replace on target machines.

### Next Steps

- [ ] Train team on using `@helcurt`
- [ ] Create custom queries for common scenarios
- [ ] Set up monitoring for critical paths
- [ ] Share success stories with team

---

**Need Help?**
- Quick Start: [QUICKSTART.md](QUICKSTART.md)
- Full Guide: [DEPLOYMENT.md](DEPLOYMENT.md)
- Configuration: [CONFIG.md](CONFIG.md)

**Version:** 1.0.0  
**Last Updated:** June 2026
