# Helcurt Conversation Template

Before starting with Helcurt, provide this information for efficient setup and monitoring.

## 📋 Quick Reference Template

Copy and fill out this template when starting a Helcurt conversation:

```
=== HELCURT SETUP REQUEST ===

🎯 Goal: [What do you want to accomplish?]
   Examples: 
   - Monitor production logs for errors
   - Deploy to new dev server
   - Check system health on multiple machines

🖥️ Target Machine(s):
   Machine 1:
     - IP/Hostname: 
     - Username: 
     - OS: Windows/Linux
     - Purpose: Production/Dev/Test
   
   Machine 2 (if applicable):
     - IP/Hostname: 
     - Username: 
     - OS: 
     - Purpose: 

📁 Folders to Monitor:
   
   Option A - Use DeltaV Areas (Recommended):
   [ ] Use Estes agent for DeltaV area-based monitoring
   
   DeltaV Areas to Monitor:
     [ ] Control Modules
     [ ] Batch Operations
     [ ] Alarms & Events
     [ ] Historical Data
     [ ] Configuration
     [ ] Diagnostics & System
     [ ] Communication & Network
     [ ] Operator Interface
     [ ] Security & Audit
     [ ] Database & Archive
     [ ] Application Station
     [ ] Device Integration (CHARMS)
     [ ] Custom: _______________
   
   Option B - Specify Manual Folders:
   Machine 1:
     - C:\Logs\Application
     - C:\ProgramData\MyApp\Logs
     - [Add more...]
   
   Machine 2:
     - [List folders...]

🔍 What to Look For (optional):
   - Error patterns: ERROR, EXCEPTION, FATAL, [custom...]
   - Warning patterns: WARNING, WARN, [custom...]
   - Specific keywords: [list if any]
   - Time range: Last hour / Last 24 hours / Since [date]

🔐 Connection Method:
   [ ] SSH (recommended)
   [ ] PowerShell Remoting (WinRM)
   [ ] Local (if running on same machine)
   [ ] Need help setting up connection

📦 Deployment Status:
   [ ] Already deployed and configured
   [ ] Need to deploy helcurt-server.exe
   [ ] Need to build executable first
   [ ] Not sure - need guidance

================================
```

## 🎯 Use Case Templates

### Use Case 1: First-Time Deployment (DeltaV Areas)

```
Hi @helcurt, I need to set up monitoring on a DeltaV server.

Target Machine:
- IP: 192.168.1.100
- Username: admin
- OS: Windows Server 2022
- Purpose: DeltaV Application Station

DeltaV Areas to Monitor (using @estes):
- Batch Operations
- Control Modules
- Alarms & Events
- Configuration

Connection: SSH (OpenSSH server is running)
Status: Need to deploy helcurt-server.exe

What to monitor: Recipe failures, control errors, critical alarms
```

### Use Case 1b: First-Time Deployment (Manual Folders)

```
Hi @helcurt, I need to set up monitoring on a new server.

Target Machine:
- IP: 192.168.1.100
- Username: admin
- OS: Windows Server 2022
- Purpose: Production web server

Folders to Monitor:
- C:\inetpub\logs\LogFiles
- C:\Logs\Application
- D:\WebApp\Logs

Connection: SSH (OpenSSH server is running)
Status: Need to deploy helcurt-server.exe

What I want to monitor:
- HTTP 500 errors
- Application exceptions
- Any critical errors in the last 24 hours
```

### Use Case 2: Check Existing Deployment

```
@helcurt check status

Target: prod-web-01 (192.168.1.100)
Already deployed and configured

Tasks:
1. Show me all errors from the last hour
2. Check system health (CPU, memory, disk)
3. Search for "connection timeout" in all logs
```

### Use Case 3: Deploy to Multiple Machines

```
Deploy Helcurt to multiple servers:

Machine 1 (Web Server 1):
- IP: 192.168.1.100
- Username: admin
- Folders: C:\inetpub\logs, C:\Logs\WebApp

Machine 2 (Web Server 2):
- IP: 192.168.1.101
- Username: admin
- Folders: C:\inetpub\logs, C:\Logs\WebApp

Machine 3 (Database Server):
- IP: 192.168.1.200
- Username: admin
- Folders: C:\Logs\Database, D:\SQLLogs

Connection: SSH (keys already set up)
Deploy to: C:\Tools\Helcurt on all machines
```

### Use Case 4: Quick Health Check

```
@helcurt health check

Targets:
- prod-web-01 (already configured)
- prod-web-02 (already configured)
- prod-db-01 (already configured)

Show me:
- System health metrics
- Any errors in last 30 minutes
- Disk space warnings
```

### Use Case 5: Custom Pattern Search

```
@helcurt search logs

Target: dev-server-01 (192.168.1.50)
Search for: "payment failed" OR "transaction error"
Time range: Last 24 hours
Folders: C:\Logs\PaymentService
Case sensitive: No
```

### Use Case 6: DeltaV Batch Investigation (with Estes)

```
@helcurt investigate batch failure

Target: deltav-appstation-01 (192.168.10.50)
Already deployed

DeltaV Areas (via @estes):
- Batch Operations (primary)
- Control Modules (related)
- Alarms & Events (context)

Search for:
- BatchID: "BATCH-2026-06-30-001"
- Recipe: "ProductionRecipe_v2.5"
- Errors: FAILED, EXCEPTION, TIMEOUT

Time range: 14:00 to 15:30 today
```

### Use Case 7: DeltaV System Health (Multi-Area)

```
@helcurt system health check

Target: deltav-prod-01 (192.168.10.100)

DeltaV Areas (via @estes):
- Diagnostics & System
- Communication & Network
- Database & Archive

Report:
- Performance metrics
- Network connectivity status
- Database health
- Any warnings or errors in last 4 hours
```

### Use Case 8: DeltaV Configuration Audit

```
@helcurt configuration audit

Target: deltav-appstation-02 (192.168.10.51)

DeltaV Areas (via @estes):
- Configuration
- Security & Audit

Time range: Last 7 days
Look for:
- User: "engineer01" OR "admin"
- Changes to: control modules, batch recipes
- Backup status: verify daily backups completed
```

### Use Case 9: Cross-Area DeltaV Troubleshooting

```
@helcurt troubleshoot production issue

Target: deltav-prod-01 (192.168.10.100)
Issue: Control module CM-001 showing intermittent faults

DeltaV Areas to scan (via @estes):
1. Control Modules (error logs)
2. Communication & Network (connectivity issues)
3. Alarms & Events (triggered alarms)
4. Diagnostics & System (system health)

Pattern: "CM-001" AND ("FAULT" OR "ERROR" OR "TIMEOUT")
Time: Last 12 hours
Priority: Critical and High severity only
```

## 📝 Information Checklist

### Required Information

#### For Deployment:
- ✅ Target machine IP or hostname
- ✅ Username for remote access
- ✅ **DeltaV Areas** (if using Estes) **OR** List of folders to monitor
- ✅ Connection method (SSH/WinRM)

#### For Monitoring (Already Deployed):
- ✅ Server name or identifier
- ✅ **DeltaV Area** (if applicable) or specific folders
- ✅ What to search for (errors/warnings/patterns)
- ✅ Optional: Time range

#### For DeltaV Systems (with Estes):
- ✅ DeltaV Areas to monitor (see @estes agent for list)
- ✅ System type (App Station, Batch Server, Operator Station, etc.)
- ✅ Specific modules or recipes (if investigating specific issues)
- ✅ Related areas for cross-area analysis

### Optional But Helpful:

- 🔹 Custom error patterns beyond defaults
- 🔹 Specific log file names
- 🔹 Time ranges for historical analysis
- 🔹 File patterns (*.log, *.txt, etc.)
- 🔹 Folders to exclude
- 🔹 Server purpose/environment (prod/dev/test)
- 🔹 **DeltaV-specific**: Batch IDs, Recipe names, Control Module names

## ❌ What NOT to Include

**Don't share:**
- ❌ Actual passwords (use secure prompts or SSH keys)
- ❌ API keys or tokens
- ❌ Sensitive application data
- ❌ Customer information

**Helcurt needs:**
- ✅ Server connection info (IP, username)
- ✅ Folder paths
- ✅ Search patterns

## 💡 Example Conversations

### ✅ Good: Complete Information

```
I need to monitor our production API server for errors.

Server: api-prod-01.company.com (10.0.1.50)
Username: monitoring-user
SSH: Already configured with key authentication

Monitor these folders:
- /var/log/api
- /var/log/nginx
- /opt/application/logs

Looking for:
- HTTP 5xx errors
- Exception stack traces
- "Database connection failed"
- Time: Last 2 hours

Helcurt is already deployed at /opt/helcurt/helcurt-server
```

### ✅ Good: DeltaV with Estes Areas

```
Monitor DeltaV production system for batch issues.

Server: deltav-prod-app01 (192.168.10.100)
Username: deltav-admin
SSH: Configured

DeltaV Areas (via Estes):
- Batch Operations
- Control Modules
- Alarms & Events

Looking for:
- Recipe failures
- Control module faults
- Critical alarms
- BatchID: "BATCH-2026-06-30-*"
- Time: Last 4 hours

Helcurt already deployed at C:\Tools\Helcurt
```

### ⚠️ Incomplete: Missing Key Info

```
Check the logs for errors on the server.
```
**Missing:** Which server? What logs? Already deployed?

### ✅ Good: Step-by-Step

```
I'm new to Helcurt. Help me set up monitoring for:

Server: 192.168.1.100 (Windows Server)
Username: admin
I have SSH access
Folders to monitor: C:\Logs\App1, C:\Logs\App2

Status: Haven't deployed anything yet
Goal: Monitor for errors and warnings
```

### ✅ Good: DeltaV Area-Based Deployment

```
I'm new to Helcurt and Estes. Set up DeltaV monitoring:

Server: 192.168.10.50 (DeltaV App Station)
Username: deltav-ops
SSH access: Yes

DeltaV Areas to monitor:
- Batch Operations (all folders)
- Configuration (audit logs)
- Security & Audit (access logs)

Status: Need to deploy helcurt-server.exe
Goal: Track batch execution and configuration changes
```

## 🚀 Quick Start Phrases

Use these to get started quickly:

**For Deployment:**
```
"Deploy Helcurt to [IP] as [username] monitoring [folders]"
"Set up monitoring on [server] for [folders]"
"Install helcurt-server.exe on [machine] at [path]"
```

**For Monitoring:**
```
"@helcurt check [server] for errors in the last [time]"
"@helcurt scan [folders] for [pattern]"
"@helcurt get health status on [servers]"
"@helcurt search logs for [keywords] on [server]"
```

**For DeltaV/Estes Areas:**
```
"@helcurt check DeltaV area: [area-name] on [server]"
"@helcurt scan Estes areas: [area1], [area2] for [pattern]"
"@helcurt deploy monitoring for DeltaV areas: [areas] to [server]"
"@estes list all DeltaV areas"
"@estes what folders are in area: [area-name]"
```

**For Multiple Operations:**
```
"Deploy to [servers] and monitor [folders]"
"Check all production servers for critical errors"
"Get health report from all configured machines"
```

## 📚 Standard Workflows

### Workflow 1: New Server Setup

1. **Provide info:**
   - Server IP/hostname
   - Username
   - Folders to monitor
   
2. **Deploy:** Use deployment script or manual steps

3. **Configure VS Code:** Add MCP server entry

4. **Test:** Run health check

### Workflow 2: Daily Monitoring

1. **Quick check:**
   ```
   @helcurt-prod check for errors in the last hour
   ```

2. **Detailed search:**
   ```
   @helcurt-prod search for "timeout" in last 24 hours
   ```

3. **Health status:**
   ```
   @helcurt-prod get system health
   ```

### Workflow 3: Incident Investigation

1. **Identify time range:**
   ```
   When did the issue occur? [timestamp]
   ```

2. **Search for patterns:**
   ```
   @helcurt-prod search for "error" OR "exception" between [start] and [end]
   ```

3. **Check related logs:**
   ```
   @helcurt-prod tail [specific-log-file] last 100 lines
   ```

## 🎓 Learning Mode

**New to Helcurt?** Start with:

```
Hi, I'm new to Helcurt. I want to:
- Monitor logs on: [server description]
- Looking for: [what problems]
- Current setup: [nothing/partially/fully deployed]

Can you guide me through the setup?
```

## 📄 Deployment Quick Form

For first-time setup, provide:

```yaml
Deployment Request:
  target_machines:
    - hostname: 192.168.1.100
      username: admin
      deploy_path: C:\Tools\Helcurt
      monitored_folders:
        - C:\Logs\App1
        - C:\Logs\App2
      
  connection:
    method: ssh  # or winrm
    authentication: key  # or password
    
  monitoring:
    error_patterns:
      - ERROR
      - CRITICAL
      - EXCEPTION
    warning_patterns:
      - WARNING
      - WARN
    
  status: new_deployment  # or existing or needs_update
```

## 🔍 Monitoring Request Form

For ongoing monitoring:

```yaml
Monitoring Request:
  servers:
    - helcurt-prod-web-01
    - helcurt-prod-web-02
    
  query_type: error_scan  # or health_check or search or tail
  
  parameters:
    time_range: last_1_hour
    patterns:
      - "connection failed"
      - "timeout"
    severity: error  # or warning or all
    
  output: summary  # or detailed or raw
```

## 🎯 Summary

**Before talking to Helcurt, have ready:**

1. 🖥️ **Target machine(s)** - IP/hostname
2. 👤 **Username** - For SSH/WinRM
3. 📁 **Folders** - What to monitor
4. 🔍 **Goal** - What you're looking for
5. 📦 **Status** - Deployed or need deployment?

**Use the templates above to structure your request!**

This ensures:
- ✅ Faster setup
- ✅ Fewer clarifying questions
- ✅ Better results
- ✅ More efficient monitoring
