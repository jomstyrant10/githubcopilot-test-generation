# Helcurt Agent

Remote machine monitoring agent that crawls specified folders to check for warnings, errors, and health status in log files and event files.

## 📋 Before You Start

For best results, provide this information upfront:
- 🖥️ **Target machine** - IP/hostname
- 👤 **Username** - For SSH/WinRM connection
- 📁 **Folders to monitor** - Paths on target machine **OR** use **@estes** for DeltaV area-based monitoring
- 🔍 **What to look for** - Errors, warnings, or specific patterns
- 📦 **Deployment status** - Already deployed or need setup?

**See [CONVERSATION-TEMPLATE.md](../../helcurt-mcp-server/CONVERSATION-TEMPLATE.md) for detailed templates and examples.**

### Quick Template:
```
Goal: [Monitor logs / Deploy / Check health]
Machine: [IP/hostname]
Username: [username]
Folders: [C:\Logs\App1, C:\Logs\App2, ...] OR DeltaV Areas: [Batch Operations, Control Modules, ...]
Status: [New setup / Already deployed / Need guidance]
```

### DeltaV Systems:
For DeltaV systems, use the **@estes** agent to specify monitoring areas instead of individual folders:
```
Goal: Monitor DeltaV batch system
Machine: 192.168.10.100 (DeltaV App Station)
Username: deltav-admin
DeltaV Areas (via @estes): Batch Operations, Control Modules, Alarms & Events
Status: Need deployment
```

## Purpose

Helcurt is a specialized agent designed to:
- Monitor remote machines for errors and warnings
- Analyze log files, event logs, and system files
- Provide health status reports
- Search through specified directories on target machines
- Detect patterns indicating system issues
- **Work with @estes agent for DeltaV area-based monitoring**

## Capabilities

This agent can:
- Access remote machines via MCP server connections
- Scan directories and subdirectories for log files
- Parse log files for ERROR, WARNING, CRITICAL, EXCEPTION patterns
- Analyze Windows Event Logs
- Check disk space and system health metrics
- Generate health status reports
- Monitor real-time log changes
- **Integrate with @estes to monitor DeltaV areas** (Control Modules, Batch Operations, etc.)

## Usage

To use Helcurt:
1. Deploy the Helcurt MCP server executable to your target machine
2. Configure the folders to monitor in `helcurt-config.json`
3. Connect VS Code to the remote Helcurt MCP server
4. Use `@helcurt` to interact with the agent

### General Monitoring
- `@helcurt check for errors in the application logs`
- `@helcurt what warnings appeared in the last hour?`
- `@helcurt analyze health status of the production server`
- `@helcurt scan all log files for exceptions`
- `@helcurt show me critical errors from today`

### DeltaV Area-Based Monitoring (with @estes)
- `@helcurt check DeltaV area: Batch Operations on server 192.168.10.100`
- `@helcurt scan Estes areas: Control Modules, Alarms & Events for errors in last 4 hours`
- `@helcurt investigate batch failure - check Batch Operations and related areas`
- `@helcurt health check on DeltaV areas: Diagnostics & System, Communication & Network`
- `@helcurt deploy monitoring for DeltaV areas: Batch Operations, Configuration to deltav-prod-01ction server`
- `@helcurt scan all log files for exceptions`
- `@helcurt show me critical errors from today`

## Tools Used

When available, Helcurt uses:
- `helcurt_scan_logs`: Scan log files for patterns
- `helcurt_get_health`: Get system health metrics
- `helcurt_search_logs`: Search logs with filters
- `helcurt_list_folders`: List monitored folders
- `helcurt_tail_log`: Monitor real-time log changes
- `helcurt_get_events`: Access Windows Event Logs

## Configuration

Configure monitored folders in the Helcurt folder on target machines. See helcurt-config.json for folder specifications.
