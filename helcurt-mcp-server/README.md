# Helcurt MCP Server

Remote machine monitoring MCP server for VS Code. Helcurt allows you to monitor log files, check system health, and analyze errors on remote Windows machines directly from VS Code.

## Features

- 📁 **Folder Monitoring**: Monitor multiple folders for log files
- 🔍 **Log Scanning**: Search for errors, warnings, and custom patterns
- 💊 **Health Checks**: Get system health metrics (CPU, memory, disk)
- 📊 **Event Logs**: Access Windows Event Logs (Windows only)
- ⚡ **Real-time**: Tail log files in real-time
- 🎯 **Flexible**: Configurable patterns and folders

## 📋 Before You Start

For efficient Helcurt setup, prepare this information:

- 🖥️ **Target machine IP/hostname**
- 👤 **Username for SSH/WinRM**
- 📁 **Folders to monitor**
- 🔍 **What to look for** (errors, warnings, patterns)
- 📦 **Deployment status** (new or existing)

**See [CONVERSATION-TEMPLATE.md](CONVERSATION-TEMPLATE.md) for detailed templates and examples.**

## Quick Start

### Building the Executable

1. **Install dependencies:**
   ```bash
   npm install
   ```

2. **Build the executable:**
   ```bash
   npm run build
   ```
   
   Or use the batch/PowerShell scripts:
   ```bash
   # Windows Command Prompt
   build.bat
   
   # PowerShell
   .\build.ps1
   ```

This creates `helcurt-server.exe` - a standalone executable that requires no Node.js installation on the target machine.

### Deployment to Target Machine

1. **Copy files to target machine:**
   - `helcurt-server.exe` (required)
   - `helcurt-config.example.json` (rename to `helcurt-config.json`)

2. **Configure monitored folders:**
   ```json
   {
     "monitoredFolders": [
       "C:\\Logs\\YourApp",
       "C:\\ProgramData\\YourApp\\Logs"
     ]
   }
   ```

3. **Test the server:**
   ```bash
   helcurt-server.exe
   ```

### VS Code Configuration

Add to your VS Code `settings.json`:

```json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt": {
      "command": "C:\\path\\to\\helcurt-server.exe",
      "args": [],
      "env": {}
    }
  }
}
```

For **remote machines**, use SSH or remote execution:

```json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt-remote": {
      "command": "ssh",
      "args": [
        "user@remote-machine",
        "C:\\path\\to\\helcurt-server.exe"
      ]
    }
  }
}
```

## Configuration

### Folder Specification

The `helcurt-config.json` file is where you specify folders to monitor. Create this file in the same directory as `helcurt-server.exe`:

```json
{
  "monitoredFolders": [
    "C:\\Logs",
    "C:\\ProgramData\\MyApp\\Logs",
    "\\\\NetworkServer\\SharedLogs"
  ],
  "logPatterns": ["*.log", "*.txt", "*.out"],
  "errorPatterns": ["ERROR", "CRITICAL", "FATAL"],
  "warningPatterns": ["WARNING", "WARN"]
}
```

**Key Configuration Options:**

| Option | Description |
|--------|-------------|
| `monitoredFolders` | Array of absolute paths to monitor |
| `logPatterns` | File patterns to scan (glob patterns) |
| `errorPatterns` | Strings to identify as errors |
| `warningPatterns` | Strings to identify as warnings |
| `excludePatterns` | Patterns to exclude from scanning |

See [CONFIG.md](CONFIG.md) for detailed configuration guide.

## Using Helcurt in VS Code

Once configured, use the `@helcurt` agent in VS Code:

### Example Queries

```
@helcurt check for errors in the last hour
@helcurt show me warnings from application logs
@helcurt get system health status
@helcurt scan all logs for "connection timeout"
@helcurt tail the latest log file
@helcurt show Windows error events from the last 30 minutes
```

## Available Tools

The Helcurt MCP server provides these tools:

- **`helcurt_scan_logs`**: Scan log files for errors/warnings/patterns
- **`helcurt_get_health`**: Get system health metrics
- **`helcurt_search_logs`**: Search logs with filters and time ranges
- **`helcurt_list_folders`**: List monitored folders
- **`helcurt_tail_log`**: Get last N lines from a log file
- **`helcurt_get_events`**: Get Windows Event Log entries

## Architecture

```
┌─────────────────┐         ┌──────────────────┐
│   VS Code       │         │ Target Machine   │
│                 │         │                  │
│  @helcurt ──────┼────────>│ helcurt-server   │
│  (Agent)        │  MCP    │    .exe          │
│                 │ Protocol│                  │
└─────────────────┘         │  ┌──────────┐    │
                            │  │  Config  │    │
                            │  └──────────┘    │
                            │                  │
                            │  ┌──────────┐    │
                            │  │   Logs   │    │
                            │  └──────────┘    │
                            └──────────────────┘
```

## Development

### Running in Development Mode

```bash
npm run dev
```

### Building from Source

```bash
# Install dependencies
npm install

# Build TypeScript
npm run build

# Create executable
npm run build:exe
```

## Permissions

The Helcurt server runs with the permissions of the user account that launches it. Ensure:

- **Read access** to all monitored folders
- **Execute permissions** for the executable
- For **Windows Event Logs**: Run as administrator or with appropriate permissions

## Troubleshooting

### Server won't start
- Check that `helcurt-config.json` exists in the same directory
- Verify JSON syntax is valid
- Check file permissions

### Cannot access folders
- Verify paths exist and are accessible
- Check user permissions for monitored folders
- For network paths, ensure network connectivity

### No logs found
- Verify `logPatterns` match your log file names
- Check `excludePatterns` aren't excluding your logs
- Ensure folders contain matching files

## Security Considerations

- **Access Control**: Helcurt runs with the permissions of its user account
- **Network**: MCP uses stdio transport - secure SSH for remote access
- **Logging**: Helcurt only reads files, never modifies them
- **Configuration**: Store config file securely, may contain sensitive paths

## License

MIT

## Support

For issues and questions:
- Check [CONFIG.md](CONFIG.md) for configuration help
- Review agent documentation in `.github/agents/helcurt.agent.md`
- Check VS Code MCP settings

## Changelog

### Version 1.0.0
- Initial release
- Support for log file scanning
- System health monitoring
- Windows Event Log access
- Configurable folder monitoring
