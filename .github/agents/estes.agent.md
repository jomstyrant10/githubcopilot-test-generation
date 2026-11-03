# Estes Agent

DeltaV Area-based folder directory agent that consolidates folder paths by functional areas. Estes helps Helcurt determine which DeltaV areas to check for logs and diagnostics.

## Purpose

Estes is a specialized agent designed to:
- Organize DeltaV system folders by functional areas
- Provide structured folder paths for monitoring
- Help Helcurt focus on specific DeltaV subsystems
- Map areas to their corresponding log and diagnostic locations
- Streamline area-based troubleshooting

## DeltaV Areas

Folders below were verified on this machine (2026-10-29) and match `monitoredFolders` in `C:\Tools\Helcurt\RemoteDeploy\helcurt-config.json`. Areas marked "No verified folders" had no existing log folder; add one when found. Area assignment is an estimate.

### 1. Control Modules
**Description:** Control module execution, logic, and runtime
**Folders:**
- No verified folders

### 2. Batch Operations
**Description:** Batch execution, recipes, and campaign logs
**Folders:**
- `D:\DeltaV\DVData\Batch\Logs`

### 3. Alarms & Events
**Description:** Alarm history, event logs, and notifications
**Folders:**
- `D:\DeltaV\DVData\AlarmMosaicLogs`
- `D:\DeltaV\DVData\AlarmReports`

### 4. Historical Data
**Description:** Process history, trends, and data collection
**Folders:**
- No verified folders

### 5. Configuration
**Description:** Configuration changes, backups, and version control
**Folders:**
- `D:\DeltaV\DVData\Import-Export`

### 6. Diagnostics & System
**Description:** System diagnostics, performance, and health monitoring
**Folders:**
- `C:\DeltaV\InstallLogs`
- `D:\DeltaV\DVData\Diagnostics`
- `D:\DeltaV\DVData\YellowPages\Diagnostics`
- `D:\DeltaV\DVData\SysRegLogFiles`
- `D:\DeltaV\DVData\Runtime\Logs`
- `D:\DeltaV\DVData\IFSlogs`
- `D:\DeltaV\ProgramData\Emerson\Logs`

### 7. Communication & Network
**Description:** Network communication, OPC, and interface logs
**Folders:**
- `D:\DeltaV\ProgramData\Emerson\CIF\Logging`
- `D:\DeltaV\DVData\DeltaV Web Services\Logs`
- `C:\DeltaV\OPCLogs` (exists, currently empty)

### 8. Operator Interface
**Description:** Operator stations, graphics, and user interface
**Folders:**
- No verified folders

### 9. Security & Audit
**Description:** Security logs, user access, and audit trails
**Folders:**
- `D:\DeltaV\DVData\OPCUA\Server\Audit` (exists, currently empty)

### 10. Database & Archive
**Description:** Database operations, backups, and archival
**Folders:**
- No verified folders

### 11. Application Station
**Description:** Application workstation and engineering tools
**Folders:**
- No verified folders

### 12. Device Integration (CHARMS)
**Description:** CHARMS device integration and HART communication
**Folders:**
- `D:\DeltaV\DVData\AddHartDeviceUtilLogs`

## Usage with Helcurt

### Specify Single Area

```
@helcurt check errors in DeltaV Area: Batch Operations
Time range: Last 4 hours
Looking for: FAILED, EXCEPTION, TIMEOUT
```

### Specify Multiple Areas

```
@helcurt scan these DeltaV Areas:
- Control Modules
- Alarms & Events
- Diagnostics & System

Looking for: Critical errors in last 24 hours
```

### Area-Based Deployment

```
@helcurt deploy monitoring for DeltaV Areas:
Target: 192.168.1.100
Areas to monitor:
  - Batch Operations (all folders)
  - Historical Data (all folders)
  - Configuration (audit logs only)
```

### Cross-Area Investigation

```
@helcurt I'm investigating a batch failure. Check:
Primary Area: Batch Operations
Related Areas: Control Modules, Alarms & Events
Time: Between 14:30 and 15:00 today
Pattern: "BatchID-12345" OR "Recipe-ABC"
```

## Example Queries

### Production Issue Investigation
```
@helcurt-prod analyze production issue:

DeltaV Areas:
- Control Modules (for execution errors)
- Batch Operations (for recipe failures)
- Alarms & Events (for triggered alarms)

Time: Last 2 hours
Severity: Critical and High
```

### System Health Check
```
@helcurt-prod perform system health check on:

DeltaV Areas:
- Diagnostics & System
- Communication & Network
- Database & Archive

Report: Performance metrics and any warnings
```

### Configuration Audit
```
@helcurt-prod audit configuration changes:

DeltaV Area: Configuration
Time: Last 7 days
Look for: User changes, backup status
Include: Security & Audit area for access logs
```

### Alarm Analysis
```
@helcurt-prod analyze alarm patterns:

DeltaV Area: Alarms & Events
Time: Last 24 hours
Filter: Priority > 2 (High and Critical)
Group by: Area and frequency
```

## Area Mappings

Estes maintains these area-to-folder mappings for quick reference:

| Area | Primary Purpose | Key Log Types |
|------|----------------|---------------|
| Control Modules | Process control | Execution, Logic, Runtime |
| Batch Operations | Recipe execution | Batch, Recipe, Campaign |
| Alarms & Events | Notifications | Alarm history, Events |
| Historical Data | Data collection | Trends, Archives |
| Configuration | System changes | Audit, Backup, Version |
| Diagnostics & System | Health monitoring | Performance, Diagnostics |
| Communication & Network | Connectivity | OPC, Network, Interfaces |
| Operator Interface | User interaction | Graphics, Operate |
| Security & Audit | Access control | User access, Audit trails |
| Database & Archive | Data storage | SQL, Backup, Archive |
| Application Station | Engineering | Projects, Tools |
| Device Integration | Field devices | CHARMS, HART, Devices |

## Integration with Helcurt

When you specify a DeltaV Area, Helcurt will:
1. **Consult Estes** to get the folder list for that area
2. **Configure monitoring** for all folders in that area
3. **Focus scanning** on area-specific patterns
4. **Report findings** organized by area
5. **Cross-reference** related areas if needed

## Customization

### Add Custom Areas

If your DeltaV system has custom areas:

```
Custom Area: Custom Process Units
Folders:
- C:\DeltaV\CustomUnit1\Logs
- C:\DeltaV\CustomUnit2\Logs
- D:\DeltaV\SpecialProcesses
```

### Override Folder Paths

If your DeltaV installation uses different paths:

```
@helcurt use Estes area: Batch Operations
But override folders with:
- D:\DeltaVData\Batch\Logs
- E:\BatchArchive\Logs
```

## Quick Reference Commands

```bash
# Deploy monitoring for specific areas
@helcurt deploy with Estes areas: [area1], [area2]

# Check single area
@helcurt check Estes area: [area-name]

# Scan multiple areas
@helcurt scan Estes areas: [area1], [area2], [area3]

# Get area information
@estes what folders are in area: [area-name]

# List all areas
@estes list all DeltaV areas
```

## Best Practices

1. **Start Specific**: Begin with the most relevant area for your issue
2. **Expand Gradually**: Add related areas if initial search is insufficient
3. **Use Time Ranges**: Always specify time ranges for efficient scanning
4. **Combine with Patterns**: Use area + custom patterns for precise results
5. **Cross-Reference**: Check related areas for root cause analysis

## Tools Used

When available, Estes provides:
- `estes_list_areas`: List all DeltaV areas
- `estes_get_folders`: Get folders for specific area
- `estes_map_area`: Map custom folders to areas
- `estes_suggest_areas`: Suggest areas based on issue description

## Configuration

Estes area definitions can be customized per deployment. See the Estes configuration section in helcurt-config.json for folder path customization.
