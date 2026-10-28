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

### 1. Control Modules
**Description:** Control module execution, logic, and runtime
**Folders:**
- `C:\DeltaV\Logs\ControlModules`
- `C:\DeltaV\Runtime\CM\Logs`
- `C:\ProgramData\DeltaV\Control\Diagnostics`
- `D:\DeltaV\CM\ErrorLogs`

### 2. Batch Operations
**Description:** Batch execution, recipes, and campaign logs
**Folders:**
- `C:\DeltaV\Batch\Logs`
- `C:\DeltaV\Batch\Recipes\Logs`
- `C:\ProgramData\DeltaV\Batch\Diagnostics`
- `D:\DeltaV\Batch\Reports`
- `C:\DeltaV\Batch\Audit`

### 3. Alarms & Events
**Description:** Alarm history, event logs, and notifications
**Folders:**
- `C:\DeltaV\Alarms\Logs`
- `C:\DeltaV\Events\History`
- `C:\ProgramData\DeltaV\Alarms\Archive`
- `D:\DeltaV\AlarmJournal`

### 4. Historical Data
**Description:** Process history, trends, and data collection
**Folders:**
- `C:\DeltaV\History\Logs`
- `C:\DeltaV\PHD\Logs`
- `D:\DeltaV\Historian\Data`
- `C:\ProgramData\DeltaV\History\Diagnostics`
- `E:\DeltaV\HistoricalArchive`

### 5. Configuration
**Description:** Configuration changes, backups, and version control
**Folders:**
- `C:\DeltaV\Config\Logs`
- `C:\DeltaV\DVExplorer\Logs`
- `C:\DeltaV\Backup\Logs`
- `C:\ProgramData\DeltaV\Configuration\Audit`
- `D:\DeltaV\ConfigBackup`

### 6. Diagnostics & System
**Description:** System diagnostics, performance, and health monitoring
**Folders:**
- `C:\DeltaV\Diagnostics`
- `C:\DeltaV\System\Logs`
- `C:\ProgramData\DeltaV\Diagnostics`
- `C:\DeltaV\Performance\Logs`
- `D:\DeltaV\SystemHealth`

### 7. Communication & Network
**Description:** Network communication, OPC, and interface logs
**Folders:**
- `C:\DeltaV\Network\Logs`
- `C:\DeltaV\OPC\Logs`
- `C:\DeltaV\Communication\Diagnostics`
- `C:\ProgramData\DeltaV\Network`
- `D:\DeltaV\Interfaces\Logs`

### 8. Operator Interface
**Description:** Operator stations, graphics, and user interface
**Folders:**
- `C:\DeltaV\Operate\Logs`
- `C:\DeltaV\Graphics\Logs`
- `C:\DeltaV\OpStation\Diagnostics`
- `C:\ProgramData\DeltaV\Operate`
- `D:\DeltaV\UserInterface\Logs`

### 9. Security & Audit
**Description:** Security logs, user access, and audit trails
**Folders:**
- `C:\DeltaV\Security\Logs`
- `C:\DeltaV\Audit\Logs`
- `C:\ProgramData\DeltaV\Security\Audit`
- `D:\DeltaV\AccessLogs`
- `C:\DeltaV\UserManagement\Logs`

### 10. Database & Archive
**Description:** Database operations, backups, and archival
**Folders:**
- `C:\DeltaV\Database\Logs`
- `C:\DeltaV\SQL\Logs`
- `C:\ProgramData\DeltaV\Database\Backup`
- `D:\DeltaV\Archive`
- `E:\DeltaV\DatabaseBackup`

### 11. Application Station
**Description:** Application workstation and engineering tools
**Folders:**
- `C:\DeltaV\AppStation\Logs`
- `C:\DeltaV\Engineering\Logs`
- `C:\ProgramData\DeltaV\AppStation\Diagnostics`
- `D:\DeltaV\Engineering\Projects`

### 12. Device Integration (CHARMS)
**Description:** CHARMS device integration and HART communication
**Folders:**
- `C:\DeltaV\CHARMS\Logs`
- `C:\DeltaV\Devices\Logs`
- `C:\ProgramData\DeltaV\CHARMS\Diagnostics`
- `D:\DeltaV\DeviceManagement`

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
