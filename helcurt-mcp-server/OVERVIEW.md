# Helcurt - Complete Overview

## What is Helcurt?

Helcurt is a **VS Code agent** that monitors remote machines for errors, warnings, and system health issues. It consists of:

1. **Agent Definition** - Tells VS Code how to use Helcurt
2. **MCP Server** - Runs on target machines, providing monitoring capabilities
3. **Configuration System** - Allows you to specify which folders to monitor

## Architecture

```
┌─────────────────────────────────────────┐
│          VS Code Workspace              │
│                                         │
│  .github/agents/helcurt.agent.md       │◄─── Agent definition
│                                         │
│  ┌─────────────────┐                   │
│  │ Copilot Chat    │                   │
│  │                 │                   │
│  │  @helcurt ───┐  │                   │
│  └──────────────┼──┘                   │
└─────────────────┼────────────────────────┘
                  │ MCP Protocol
                  │
        ┌─────────▼──────────────────────────┐
        │     Target Machine (Remote)        │
        │                                    │
        │  C:\Tools\Helcurt\                 │
        │    ├── helcurt-server.exe  ◄────── Executable (run this)
        │    └── helcurt-config.json ◄────── Folder config (edit this)
        │                                    │
        │  Monitored Folders:                │
        │    ├── C:\Logs\App1\               │
        │    ├── C:\Logs\App2\               │
        │    └── C:\ProgramData\...\Logs\    │
        └────────────────────────────────────┘
```

## File Structure

### In Your Workspace

```
githubcopilot-test-generation/
├── .github/
│   └── agents/
│       └── helcurt.agent.md          ← Agent definition
│
└── helcurt-mcp-server/               ← MCP Server source
    ├── src/
    │   └── index.ts                  ← Server implementation
    ├── package.json                  ← Dependencies
    ├── tsconfig.json                 ← TypeScript config
    ├── build.bat                     ← Windows build script
    ├── build.ps1                     ← PowerShell build script
    ├── build-exe.js                  ← Build automation
    ├── helcurt-config.example.json   ← Example config
    ├── README.md                     ← Full documentation
    ├── QUICKSTART.md                 ← 5-minute guide
    ├── DEPLOYMENT.md                 ← Deployment guide
    └── CONFIG.md                     ← Configuration guide
```

### On Target Machine (After Deployment)

```
C:\Tools\Helcurt\                     ← Can be any location
├── helcurt-server.exe                ← The executable (deploy this)
└── helcurt-config.json               ← Your folder configuration
```

## Components Explained

### 1. Agent Definition (`.github/agents/helcurt.agent.md`)

**Purpose:** Tells VS Code and Copilot about the Helcurt agent

**Key Contents:**
- Agent description and purpose
- Example queries you can use
- List of tools the agent provides
- Usage instructions

**You interact with this via:** `@helcurt` in Copilot Chat

### 2. MCP Server (`helcurt-mcp-server/`)

**Purpose:** The actual monitoring engine that runs on target machines

**Key Components:**
- `src/index.ts` - Core server logic
- `package.json` - Node.js project config
- `tsconfig.json` - TypeScript compiler settings
- Build scripts - Convert TypeScript to standalone `.exe`

**Provides these tools:**
- `helcurt_scan_logs` - Scan for errors/warnings
- `helcurt_get_health` - System health metrics
- `helcurt_search_logs` - Search with filters
- `helcurt_list_folders` - Show monitored folders
- `helcurt_tail_log` - View last N lines
- `helcurt_get_events` - Windows Event Logs

### 3. Configuration System (`helcurt-config.json`)

**Purpose:** Specify which folders Helcurt should monitor

**Location:** On the target machine, same directory as `helcurt-server.exe`

**Structure:**
```json
{
  "monitoredFolders": [
    "C:\\Logs\\Application",
    "C:\\ProgramData\\MyApp\\Logs"
  ],
  "logPatterns": ["*.log", "*.txt"],
  "errorPatterns": ["ERROR", "CRITICAL"],
  "warningPatterns": ["WARNING", "WARN"],
  "excludePatterns": ["*.tmp", "archive/**"]
}
```

**This is where you add folders to monitor!**

## How It All Works Together

### 1. Building Phase (One Time)

```bash
cd helcurt-mcp-server
npm install
npm run build
```

**Creates:** `helcurt-server.exe` (standalone, no Node.js needed on target)

### 2. Deployment Phase (Per Target Machine)

1. Copy `helcurt-server.exe` to target machine
2. Copy `helcurt-config.example.json` → rename to `helcurt-config.json`
3. Edit `helcurt-config.json` to add your folders

### 3. VS Code Configuration (One Time)

Add to `settings.json`:

```json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt": {
      "command": "C:\\Tools\\Helcurt\\helcurt-server.exe"
    }
  }
}
```

Or for remote:

```json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt-prod": {
      "command": "ssh",
      "args": ["admin@prod-server", "C:\\Tools\\Helcurt\\helcurt-server.exe"]
    }
  }
}
```

### 4. Usage Phase (Daily Use)

In VS Code Copilot Chat:

```
@helcurt check for errors in the last hour
@helcurt scan logs for "connection failed"
@helcurt get system health status
```

## Communication Flow

```
User types:
  "@helcurt check for errors"
          ↓
  VS Code Copilot sees "@helcurt"
          ↓
  Loads .github/agents/helcurt.agent.md
          ↓
  Finds MCP server in settings.json
          ↓
  Launches helcurt-server.exe (via command/SSH)
          ↓
  Server reads helcurt-config.json
          ↓
  Server scans monitored folders
          ↓
  Server returns results
          ↓
  Copilot formats and shows results
          ↓
  User sees the output
```

## Adding Folders - The Easy Way

**Q: Where do I specify which folders to monitor?**

**A:** In `helcurt-config.json` on the target machine!

**Example:**

```json
{
  "monitoredFolders": [
    "C:\\Logs\\WebApp",
    "C:\\Logs\\Database",
    "D:\\AppData\\Logs",
    "\\\\FileServer\\SharedLogs"
  ]
}
```

Just edit this file and Helcurt will monitor those folders.

## Multiple Machines

To monitor multiple machines, create multiple MCP server entries:

```json
{
  "github.copilot.chat.mcp.servers": {
    "helcurt-web1": {
      "command": "ssh",
      "args": ["admin@web1", "C:\\Tools\\Helcurt\\helcurt-server.exe"]
    },
    "helcurt-web2": {
      "command": "ssh",
      "args": ["admin@web2", "C:\\Tools\\Helcurt\\helcurt-server.exe"]
    },
    "helcurt-db": {
      "command": "ssh",
      "args": ["admin@db", "C:\\Tools\\Helcurt\\helcurt-server.exe"]
    }
  }
}
```

Each machine has its own `helcurt-config.json` with its own monitored folders.

## Key Concepts

### MCP (Model Context Protocol)
- Standard protocol for AI tools to interact with external systems
- Uses stdio (stdin/stdout) for communication
- Helcurt is an MCP server

### Agent
- VS Code feature that gives Copilot specialized capabilities
- Defined in `.agent.md` files
- Invoked with `@agent-name`

### Tools
- Functions that agents can call
- Helcurt provides 6 tools for monitoring
- Called automatically by Copilot based on your query

## What Each File Does

| File | Purpose | Edit This? |
|------|---------|-----------|
| `.github/agents/helcurt.agent.md` | Agent definition | No (unless customizing) |
| `helcurt-mcp-server/src/index.ts` | Server code | No (unless adding features) |
| `helcurt-mcp-server/package.json` | Node dependencies | No |
| `helcurt-server.exe` | Built executable | No (deploy this) |
| `helcurt-config.json` | **Folder configuration** | **Yes! Add your folders here** |
| VS Code `settings.json` | MCP connection config | Yes (add server entry) |

## Common Workflows

### Monitoring Production Logs

1. Deploy `helcurt-server.exe` to production server
2. Create `helcurt-config.json` with production log folders
3. Add to VS Code settings with SSH connection
4. Query: `@helcurt-prod check for critical errors today`

### Local Development Testing

1. Build and run locally
2. Monitor local log folders
3. Query: `@helcurt scan for exceptions in the last 10 minutes`

### Multiple Environment Monitoring

1. Deploy to dev, staging, prod
2. Different `helcurt-config.json` on each
3. Add all to VS Code settings
4. Query each: `@helcurt-dev`, `@helcurt-staging`, `@helcurt-prod`

## Security Notes

- **Read-Only:** Helcurt only reads files, never writes
- **Permissions:** Runs with permissions of the user account
- **Network:** Uses SSH for remote access (secure)
- **No Inbound Ports:** No firewall changes needed
- **Config Security:** `helcurt-config.json` may contain sensitive paths

## Troubleshooting Quick Reference

| Issue | Solution |
|-------|----------|
| Server won't start | Check `helcurt-config.json` syntax |
| No logs found | Verify paths in `helcurt-config.json` |
| Cannot connect from VS Code | Restart VS Code after settings change |
| Permission denied | Check folder read permissions |
| Agent not found | Verify `.github/agents/helcurt.agent.md` exists |

## Documentation Map

- **QUICKSTART.md** - Get running in 5 minutes
- **README.md** - Complete reference
- **DEPLOYMENT.md** - Advanced deployment scenarios
- **CONFIG.md** - Configuration options explained
- **helcurt.agent.md** - Agent definition

## Need Help?

1. **Quick questions**: See [QUICKSTART.md](QUICKSTART.md)
2. **Configuration**: See [CONFIG.md](CONFIG.md)
3. **Deployment issues**: See [DEPLOYMENT.md](DEPLOYMENT.md)
4. **Full reference**: See [README.md](README.md)

## Summary

**You have:**
- ✅ Agent definition in `.github/agents/`
- ✅ MCP server source in `helcurt-mcp-server/`
- ✅ Build scripts for creating executable
- ✅ Configuration system for folders
- ✅ Complete documentation

**To use:**
1. Build the executable
2. Deploy to target machine
3. Configure folders in `helcurt-config.json`
4. Add to VS Code settings
5. Use `@helcurt` in Copilot Chat

**Folder configuration location:**
`helcurt-config.json` on the target machine, same directory as `helcurt-server.exe`
