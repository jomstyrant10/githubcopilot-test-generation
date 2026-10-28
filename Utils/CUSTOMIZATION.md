# Example: Adding Custom Bug Types

This guide shows you how to customize the bug report extraction tool for your specific needs.

## Adding a New Bug Type

Let's say you want to add a new bug type for "performance-issues". Here's how:

### 1. Edit the Configuration

Open `Utils/bug-report-config.json` and add a new entry under `bugTypes`:

```json
{
  "bugTypes": {
    "performance-issues": {
      "description": "When experiencing slow test generation or UI lag",
      "logPaths": [
        "%APPDATA%\\Code\\logs\\window*.log",
        "%TEMP%\\vscode-copilot-*.log",
        ".vscode\\extensions.json",
        "Output\\*.csv"
      ],
      "includePatterns": [
        "*.log",
        "*.json",
        "*.csv"
      ],
      "maxFileSizeMB": 25,
      "outputPrefix": "perf-issue"
    }
  }
}
```

### 2. Explanation of Fields

| Field | Purpose | Example |
|-------|---------|---------|
| `description` | What this bug type is for | "When experiencing slow..." |
| `logPaths` | Where to find files | `["%APPDATA%\\Code\\logs"]` |
| `includePatterns` | Which file types to collect | `["*.log", "*.json"]` |
| `maxFileSizeMB` | Maximum file size to include | `25` |
| `outputPrefix` | Prefix for output folder | `"perf-issue"` |

### 3. Path Syntax

#### Environment Variables
- **Windows:** `%VARNAME%`
- **Unix/Mac:** `$VARNAME` or `${VARNAME}`

Common variables:
- `%APPDATA%` - User's application data
- `%USERPROFILE%` - User's home directory
- `%TEMP%` - Temporary files
- `%ProgramFiles%` - Program Files directory

#### Wildcards
- `*` - Matches any characters
- `**` - Recursive directory match
- `?` - Matches single character

Examples:
```json
"logPaths": [
  "logs/**/*.log",          // All .log files recursively
  "output/*-2024-*.csv",    // CSV files with date pattern
  "%TEMP%/app-*.log"        // Temp files with prefix
]
```

### 4. Test Your Configuration

```bash
# List to verify it appears
python Utils/extract-bug-logs.py --list

# Test extraction
python Utils/extract-bug-logs.py performance-issues --no-compress

# Check the output directory to verify correct files are collected
```

## Complete Example: Network Issues

Here's a complete example for tracking network-related issues:

```json
{
  "bugTypes": {
    "network-timeout": {
      "description": "When experiencing connection timeouts with Azure DevOps or Copilot API",
      "logPaths": [
        "%APPDATA%\\Code\\logs\\network*.log",
        "%APPDATA%\\Code\\logs\\exthost*.log",
        ".vscode\\mcp.json",
        "%USERPROFILE%\\.vscode\\extensions\\github.copilot-*\\dist\\extension.js.map"
      ],
      "includePatterns": [
        "*.log",
        "*.json",
        "*.map"
      ],
      "maxFileSizeMB": 15,
      "outputPrefix": "network-timeout"
    }
  }
}
```

## Tips for Creating Good Bug Types

### 1. Be Specific
✅ Good: `authentication-failure`  
❌ Bad: `error`

### 2. Include Context
Collect configuration files along with logs:
```json
"logPaths": [
  "path/to/logs/*.log",      // The logs
  "path/to/config/*.json"     // Related config
]
```

### 3. Set Reasonable Size Limits
- Small issues (config problems): 5-10 MB
- Medium issues (generation errors): 10-20 MB
- Large issues (crashes, performance): 20-50 MB

### 4. Use Descriptive Prefixes
The prefix becomes part of the output filename:
- `outputPrefix: "auth-fail"` → `auth-fail_20260604_143022.zip`

### 5. Consider Privacy
Avoid collecting:
- Password files
- API keys
- Personal data
- Proprietary code

## Common Patterns

### Collect Recent Files Only

Use wildcards with date patterns:
```json
"logPaths": [
  "logs/*-2024-*.log"
]
```

### Collect from Multiple Extensions

```json
"logPaths": [
  "%USERPROFILE%\\.vscode\\extensions\\github.copilot-*",
  "%USERPROFILE%\\.vscode\\extensions\\github.copilot-chat-*",
  "%USERPROFILE%\\.vscode\\extensions\\ms-vscode.*"
]
```

### Collect Workspace-Specific Files

```json
"logPaths": [
  ".vscode/**",
  ".github/**/*.md",
  "Output/*.csv"
]
```

## Global Settings

You can also customize global behavior:

```json
{
  "globalSettings": {
    "outputDirectory": "MyBugReports",
    "createTimestampedFolder": true,
    "includeSystemInfo": true,
    "compressOutput": true
  }
}
```

### Settings Explanation

| Setting | Default | Purpose |
|---------|---------|---------|
| `outputDirectory` | `"BugReports"` | Where to save files |
| `createTimestampedFolder` | `true` | Add timestamp to folders |
| `includeSystemInfo` | `true` | Include system info JSON |
| `compressOutput` | `true` | Create ZIP file |

## Validation Checklist

Before using your custom configuration:

- [ ] Valid JSON syntax (no trailing commas!)
- [ ] All paths use correct separators (`\\` for Windows in JSON)
- [ ] Environment variables are correctly formatted
- [ ] File size limits are reasonable
- [ ] Test with `--no-compress` first to verify files
- [ ] Check output doesn't include sensitive data

## Testing Configuration

```bash
# Validate JSON syntax
python -m json.tool Utils/bug-report-config.json

# List all bug types
python Utils/extract-bug-logs.py --list

# Test without compression to inspect files
python Utils/extract-bug-logs.py your-bug-type --no-compress

# Check the output
ls BugReports/
```

## Sharing Configurations

If you create useful bug types, consider:
1. Fork the repository
2. Add your configurations
3. Submit a pull request
4. Help others debug similar issues!

---

**Need help?** Check the main [Utils/README.md](README.md) or open an issue on GitHub.
