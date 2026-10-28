# 🐛 Bug Report Log Extraction Tool

A comprehensive utility for extracting and organizing log files when reporting bugs in the GitHubCopilot-Test-Generation project. This tool automatically collects relevant logs, configuration files, and system information based on the type of bug you're encountering.

---

## 📋 Features

- **🎯 Bug Type-Specific Collection** – Different bug types collect different sets of logs
- **⚙️ Customizable Configuration** – Easy JSON-based configuration for paths and patterns
- **📦 Automatic Compression** – Creates ZIP files ready to attach to bug reports
- **🔍 Smart File Filtering** – Filters by file patterns and size limits
- **💻 Cross-Platform** – Python script works on Windows, macOS, and Linux
- **🪟 Windows PowerShell** – Native PowerShell version for Windows users
- **📊 System Information** – Automatically includes system details for debugging

---

## 🚀 Quick Start

### Python Version (Cross-Platform)

```bash
# List available bug types
python Utils/extract-bug-logs.py --list

# Extract logs for test generation failure
python Utils/extract-bug-logs.py test-generation-failure

# Extract logs without compression
python Utils/extract-bug-logs.py general --no-compress
```

### PowerShell Version (Windows)

```powershell
# List available bug types
.\Utils\extract-bug-logs.ps1 -List

# Extract logs for test generation failure
.\Utils\extract-bug-logs.ps1 test-generation-failure

# Extract logs without compression
.\Utils\extract-bug-logs.ps1 general -NoCompress
```

---

## 🎯 Available Bug Types

### 1. `test-generation-failure`
**Use when:** Test case generation is not working properly or producing errors

**Collects:**
- VS Code logs
- GitHub Copilot extension logs
- Temporary copilot logs

### 2. `output-format-issue`
**Use when:** Generated CSV files have formatting problems or incorrect structure

**Collects:**
- Output CSV files
- VS Code logs
- Configuration files

### 3. `copilot-mcp-error`
**Use when:** Azure DevOps MCP integration is failing or returning errors

**Collects:**
- MCP configuration
- Copilot chat session logs
- VS Code logs
- MCP error logs

### 4. `instruction-not-working`
**Use when:** Instructions or prompts are not being followed correctly

**Collects:**
- Instruction files
- Prompt templates
- Copilot chat logs
- Configuration

### 5. `general`
**Use when:** You're experiencing other bugs or unexpected behavior

**Collects:**
- All VS Code logs
- Configuration files
- Output files

---

## ⚙️ Configuration

The tool's behavior is controlled by [`bug-report-config.json`](bug-report-config.json). You can customize:

### Bug Type Settings

```json
{
  "bugTypes": {
    "your-custom-type": {
      "description": "Description of when to use this type",
      "logPaths": [
        "path/to/logs",
        "%APPDATA%\\path\\with\\env\\variables",
        "path/with/**/*.wildcards"
      ],
      "includePatterns": ["*.log", "*.json"],
      "maxFileSizeMB": 10,
      "outputPrefix": "custom-prefix"
    }
  }
}
```

#### Path Expansion

- **Environment Variables:** Use `%VAR%` (Windows) or `$VAR` (Unix)
- **Wildcards:** Use `*` for any characters, `**` for recursive directory matching
- **Relative Paths:** Relative to workspace root
- **Absolute Paths:** Full paths are supported

### Global Settings

```json
{
  "globalSettings": {
    "outputDirectory": "BugReports",
    "createTimestampedFolder": true,
    "includeSystemInfo": true,
    "compressOutput": true
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `outputDirectory` | Where to save extracted logs | `BugReports` |
| `createTimestampedFolder` | Add timestamp to folder name | `true` |
| `includeSystemInfo` | Include system_info.json | `true` |
| `compressOutput` | Create ZIP file | `true` |

---

## 📁 Output Structure

### Compressed Output (default)

```
BugReports/
└── test-generation-failure_20260604_143022.zip
    ├── system_info.json
    └── logs/
        ├── copilot.log
        ├── main.log
        └── ... other files ...
```

### Uncompressed Output (--no-compress)

```
BugReports/
└── test-generation-failure_20260604_143022/
    ├── system_info.json
    └── logs/
        ├── copilot.log
        ├── main.log
        └── ... other files ...
```

---

## 🛠️ Advanced Usage

### Custom Configuration File

```bash
# Python
python Utils/extract-bug-logs.py test-gen --config custom-config.json

# PowerShell
.\Utils\extract-bug-logs.ps1 test-gen -Config custom-config.json
```

### Adding Your Own Bug Type

1. Open `Utils/bug-report-config.json`
2. Add a new entry under `bugTypes`:

```json
{
  "bugTypes": {
    "my-custom-bug": {
      "description": "When my specific issue occurs",
      "logPaths": [
        ".vscode/my-logs/*.log",
        "%TEMP%/my-app-*.log"
      ],
      "includePatterns": ["*.log", "*.txt"],
      "maxFileSizeMB": 15,
      "outputPrefix": "custom-bug"
    }
  }
}
```

3. Use it:

```bash
python Utils/extract-bug-logs.py my-custom-bug
```

---

## 📝 System Information Included

The `system_info.json` file contains:

- Timestamp of extraction
- Operating system and version
- Python/PowerShell version
- Machine name and processor
- Workspace root path

This helps maintainers understand your environment when debugging.

---

## 🔒 Privacy & Security

**⚠️ Important:** Review the extracted files before sharing!

The tool collects logs that may contain:
- File paths from your system
- User names
- Workspace information
- Debug messages

**Recommendations:**
1. Always review the ZIP file contents before attaching to bug reports
2. Redact any sensitive information
3. Use the `--no-compress` option to inspect files before compression
4. Customize `bug-report-config.json` to exclude sensitive paths

---

## 🐍 Requirements

### Python Version
- Python 3.7 or later
- No external dependencies (uses only standard library)

### PowerShell Version
- PowerShell 5.1 or later (included with Windows 10/11)
- Windows-only (uses WMI for system information)

---

## 💡 Tips & Tricks

### Reduce Output Size

If the output is too large, adjust these settings:

```json
{
  "maxFileSizeMB": 5,  // Reduce max file size
  "includePatterns": ["*.log"]  // Be more specific
}
```

### Debug Collection Issues

Use uncompressed output to see what's being collected:

```bash
python Utils/extract-bug-logs.py general --no-compress
```

### Quick Collection

For fastest results, disable system info:

```json
{
  "globalSettings": {
    "includeSystemInfo": false
  }
}
```

---

## 🤝 Contributing

Found a common bug pattern? Add it to the configuration:

1. Edit `bug-report-config.json`
2. Add a descriptive bug type
3. Submit a PR with your addition

---

## 📞 Support

If you encounter issues with the extraction tool itself:

1. Check the configuration file is valid JSON
2. Ensure paths exist and are accessible
3. Verify you have read permissions for log directories
4. Try the `general` bug type as a fallback

---

## 📄 License

This tool is part of the GitHubCopilot-Test-Generation project and follows the same license.

---

**Happy Bug Reporting! 🐛✨**
