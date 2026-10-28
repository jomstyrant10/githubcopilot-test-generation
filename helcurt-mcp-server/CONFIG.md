# Configuration Guide for Helcurt

This folder contains the configuration for which folders and files Helcurt should monitor.

## Setup Instructions

1. **Copy the example configuration:**
   ```
   copy helcurt-config.example.json helcurt-config.json
   ```

2. **Edit `helcurt-config.json` to specify your monitored folders:**

   ```json
   {
     "monitoredFolders": [
       "C:\\Logs",
       "C:\\Your\\Custom\\Path"
     ]
   }
   ```

## Configuration Options

### monitoredFolders
Array of absolute paths to folders that Helcurt should monitor.

**Example:**
```json
"monitoredFolders": [
  "C:\\Logs\\Application",
  "C:\\ProgramData\\MyApp\\Logs",
  "C:\\inetpub\\logs\\LogFiles"
]
```

### logPatterns
File patterns to scan (glob patterns).

**Example:**
```json
"logPatterns": [
  "*.log",
  "*.txt",
  "*.out",
  "error-*.log"
]
```

### errorPatterns
Strings to identify as errors when scanning logs.

**Example:**
```json
"errorPatterns": [
  "ERROR",
  "CRITICAL",
  "FATAL",
  "EXCEPTION"
]
```

### warningPatterns
Strings to identify as warnings when scanning logs.

**Example:**
```json
"warningPatterns": [
  "WARNING",
  "WARN",
  "CAUTION"
]
```

### excludePatterns
Patterns to exclude from scanning (glob patterns).

**Example:**
```json
"excludePatterns": [
  "node_modules/**",
  "*.tmp",
  "archive/**"
]
```

## Adding Folders to Monitor

Simply add the full path to any folder you want to monitor to the `monitoredFolders` array:

```json
{
  "monitoredFolders": [
    "C:\\Logs\\Production",
    "D:\\Data\\AppLogs",
    "\\\\NetworkShare\\Logs"
  ]
}
```

**Note:** Use double backslashes (`\\`) in JSON for Windows paths.

## Network Paths

You can monitor network shares by using UNC paths:

```json
"monitoredFolders": [
  "\\\\server\\share\\logs"
]
```

Ensure the user account running Helcurt has access to these network paths.
