#!/usr/bin/env node

/**
 * Helcurt MCP Server
 * Remote machine monitoring server for VS Code
 */

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  Tool
} from '@modelcontextprotocol/sdk/types.js';
import * as fs from 'fs/promises';
import * as path from 'path';
import * as os from 'os';
import fg from 'fast-glob';
import chokidar from 'chokidar';

// Configuration types
interface HelcurtConfig {
  monitoredFolders: string[];
  logPatterns: string[];
  errorPatterns: string[];
  warningPatterns: string[];
  excludePatterns: string[];
}

// Load configuration
let config: HelcurtConfig = {
  monitoredFolders: [],
  logPatterns: ['*.log', '*.txt', '*.out'],
  errorPatterns: ['ERROR', 'CRITICAL', 'FATAL', 'EXCEPTION', 'FAILED'],
  warningPatterns: ['WARNING', 'WARN', 'CAUTION'],
  excludePatterns: ['node_modules/**', '.git/**', 'dist/**']
};

async function loadConfig(): Promise<void> {
  try {
    const configPath = path.join(process.cwd(), 'helcurt-config.json');
    const configData = await fs.readFile(configPath, 'utf-8');
    const loadedConfig = JSON.parse(configData);
    config = { ...config, ...loadedConfig };
    console.error('Loaded configuration from:', configPath);
  } catch (error) {
    console.error('Using default configuration. Create helcurt-config.json to customize.');
  }
}

// Tool definitions
const TOOLS: Tool[] = [
  {
    name: 'helcurt_scan_logs',
    description: 'Scan log files in monitored folders for errors, warnings, and patterns',
    inputSchema: {
      type: 'object',
      properties: {
        pattern: {
          type: 'string',
          description: 'Search pattern (ERROR, WARNING, or custom regex)'
        },
        folder: {
          type: 'string',
          description: 'Specific folder to scan (optional, scans all if not specified)'
        },
        lastMinutes: {
          type: 'number',
          description: 'Only scan files modified in the last N minutes (optional)'
        }
      }
    }
  },
  {
    name: 'helcurt_get_health',
    description: 'Get system health metrics including disk space, memory, and recent errors',
    inputSchema: {
      type: 'object',
      properties: {}
    }
  },
  {
    name: 'helcurt_search_logs',
    description: 'Search through log files with filters and time ranges',
    inputSchema: {
      type: 'object',
      properties: {
        query: {
          type: 'string',
          description: 'Search query string'
        },
        startTime: {
          type: 'string',
          description: 'Start time (ISO 8601 format)'
        },
        endTime: {
          type: 'string',
          description: 'End time (ISO 8601 format)'
        },
        caseSensitive: {
          type: 'boolean',
          description: 'Case sensitive search (default: false)'
        }
      },
      required: ['query']
    }
  },
  {
    name: 'helcurt_list_folders',
    description: 'List all monitored folders and their configuration',
    inputSchema: {
      type: 'object',
      properties: {}
    }
  },
  {
    name: 'helcurt_tail_log',
    description: 'Get the last N lines from a specific log file',
    inputSchema: {
      type: 'object',
      properties: {
        filePath: {
          type: 'string',
          description: 'Path to the log file'
        },
        lines: {
          type: 'number',
          description: 'Number of lines to retrieve (default: 50)'
        }
      },
      required: ['filePath']
    }
  },
  {
    name: 'helcurt_get_events',
    description: 'Get Windows Event Log entries (Windows only)',
    inputSchema: {
      type: 'object',
      properties: {
        logName: {
          type: 'string',
          description: 'Event log name (Application, System, Security, etc.)'
        },
        level: {
          type: 'string',
          description: 'Event level (Error, Warning, Information)'
        },
        lastMinutes: {
          type: 'number',
          description: 'Get events from last N minutes (default: 60)'
        }
      }
    }
  }
];

// Tool implementations
async function scanLogs(args: any): Promise<string> {
  const { pattern = 'ERROR', folder, lastMinutes } = args;
  const results: any[] = [];
  
  const foldersToScan = folder ? [folder] : config.monitoredFolders;
  
  if (foldersToScan.length === 0) {
    return JSON.stringify({
      error: 'No monitored folders configured. Please add folders in helcurt-config.json'
    }, null, 2);
  }

  for (const baseFolder of foldersToScan) {
    try {
      // Check if folder exists
      await fs.access(baseFolder);
      
      // Find log files
      const logFiles = await fg(config.logPatterns.map(p => path.join(baseFolder, '**', p)), {
        ignore: config.excludePatterns,
        onlyFiles: true,
        absolute: true
      });

      for (const filePath of logFiles) {
        try {
          const stats = await fs.stat(filePath);
          
          // Check if file was modified recently
          if (lastMinutes) {
            const cutoffTime = Date.now() - (lastMinutes * 60 * 1000);
            if (stats.mtimeMs < cutoffTime) {
              continue;
            }
          }

          const content = await fs.readFile(filePath, 'utf-8');
          const lines = content.split('\n');
          
          const regex = new RegExp(pattern, 'gi');
          const matches: any[] = [];
          
          lines.forEach((line, index) => {
            if (regex.test(line)) {
              matches.push({
                lineNumber: index + 1,
                content: line.trim()
              });
            }
          });

          if (matches.length > 0) {
            results.push({
              file: filePath,
              lastModified: stats.mtime.toISOString(),
              matchCount: matches.length,
              matches: matches.slice(0, 10) // Limit to first 10 matches
            });
          }
        } catch (fileError: any) {
          console.error(`Error reading file ${filePath}:`, fileError.message);
        }
      }
    } catch (folderError: any) {
      results.push({
        folder: baseFolder,
        error: `Cannot access folder: ${folderError.message}`
      });
    }
  }

  return JSON.stringify({
    pattern,
    scannedFolders: foldersToScan,
    resultsCount: results.length,
    results
  }, null, 2);
}

async function getHealth(): Promise<string> {
  const health: any = {
    timestamp: new Date().toISOString(),
    hostname: os.hostname(),
    platform: os.platform(),
    uptime: os.uptime(),
    memory: {
      total: os.totalmem(),
      free: os.freemem(),
      used: os.totalmem() - os.freemem(),
      usedPercent: ((os.totalmem() - os.freemem()) / os.totalmem() * 100).toFixed(2)
    },
    cpu: {
      model: os.cpus()[0]?.model || 'Unknown',
      cores: os.cpus().length,
      loadAvg: os.loadavg()
    },
    monitoredFolders: config.monitoredFolders,
    diskSpace: []
  };

  // Check disk space for monitored folders
  for (const folder of config.monitoredFolders) {
    try {
      await fs.access(folder);
      health.diskSpace.push({
        folder,
        accessible: true
      });
    } catch {
      health.diskSpace.push({
        folder,
        accessible: false
      });
    }
  }

  return JSON.stringify(health, null, 2);
}

async function searchLogs(args: any): Promise<string> {
  const { query, startTime, endTime, caseSensitive = false } = args;
  const results: any[] = [];
  
  const flags = caseSensitive ? 'g' : 'gi';
  const regex = new RegExp(query, flags);
  
  for (const baseFolder of config.monitoredFolders) {
    try {
      const logFiles = await fg(config.logPatterns.map(p => path.join(baseFolder, '**', p)), {
        ignore: config.excludePatterns,
        onlyFiles: true,
        absolute: true
      });

      for (const filePath of logFiles) {
        try {
          const stats = await fs.stat(filePath);
          
          // Check time range
          if (startTime && new Date(stats.mtime) < new Date(startTime)) continue;
          if (endTime && new Date(stats.mtime) > new Date(endTime)) continue;

          const content = await fs.readFile(filePath, 'utf-8');
          const lines = content.split('\n');
          const matches: any[] = [];

          lines.forEach((line, index) => {
            if (regex.test(line)) {
              matches.push({
                lineNumber: index + 1,
                content: line.trim()
              });
            }
          });

          if (matches.length > 0) {
            results.push({
              file: filePath,
              lastModified: stats.mtime.toISOString(),
              matchCount: matches.length,
              matches: matches.slice(0, 20)
            });
          }
        } catch (error: any) {
          console.error(`Error reading ${filePath}:`, error.message);
        }
      }
    } catch (error: any) {
      console.error(`Error scanning ${baseFolder}:`, error.message);
    }
  }

  return JSON.stringify({
    query,
    caseSensitive,
    timeRange: { startTime, endTime },
    resultsCount: results.length,
    results
  }, null, 2);
}

async function listFolders(): Promise<string> {
  return JSON.stringify({
    monitoredFolders: config.monitoredFolders,
    logPatterns: config.logPatterns,
    errorPatterns: config.errorPatterns,
    warningPatterns: config.warningPatterns,
    excludePatterns: config.excludePatterns
  }, null, 2);
}

async function tailLog(args: any): Promise<string> {
  const { filePath, lines = 50 } = args;
  
  try {
    const content = await fs.readFile(filePath, 'utf-8');
    const allLines = content.split('\n');
    const tailLines = allLines.slice(-lines);
    
    return JSON.stringify({
      file: filePath,
      totalLines: allLines.length,
      displayedLines: tailLines.length,
      lines: tailLines
    }, null, 2);
  } catch (error: any) {
    return JSON.stringify({
      error: `Cannot read file: ${error.message}`
    }, null, 2);
  }
}

async function getEvents(args: any): Promise<string> {
  const { logName = 'Application', level = 'Error', lastMinutes = 60 } = args;
  
  if (os.platform() !== 'win32') {
    return JSON.stringify({
      error: 'Windows Event Logs are only available on Windows systems'
    }, null, 2);
  }

  // Note: This requires PowerShell on Windows
  try {
    const { execSync } = await import('child_process');
    const startTime = new Date(Date.now() - lastMinutes * 60 * 1000).toISOString();
    
    const psScript = `Get-EventLog -LogName ${logName} -EntryType ${level} -After '${startTime}' | Select-Object -First 50 | ConvertTo-Json`;
    const output = execSync(`powershell -Command "${psScript}"`, { encoding: 'utf-8' });
    
    return JSON.stringify({
      logName,
      level,
      lastMinutes,
      events: JSON.parse(output)
    }, null, 2);
  } catch (error: any) {
    return JSON.stringify({
      error: `Cannot access Windows Event Log: ${error.message}`
    }, null, 2);
  }
}

// Main server setup
async function main() {
  // Load configuration
  await loadConfig();

  const server = new Server(
    {
      name: 'helcurt-mcp-server',
      version: '1.0.0',
    },
    {
      capabilities: {
        tools: {},
      },
    }
  );

  // List tools handler
  server.setRequestHandler(ListToolsRequestSchema, async () => {
    return { tools: TOOLS };
  });

  // Call tool handler
  server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;

    try {
      switch (name) {
        case 'helcurt_scan_logs':
          return { content: [{ type: 'text', text: await scanLogs(args) }] };
        
        case 'helcurt_get_health':
          return { content: [{ type: 'text', text: await getHealth() }] };
        
        case 'helcurt_search_logs':
          return { content: [{ type: 'text', text: await searchLogs(args) }] };
        
        case 'helcurt_list_folders':
          return { content: [{ type: 'text', text: await listFolders() }] };
        
        case 'helcurt_tail_log':
          return { content: [{ type: 'text', text: await tailLog(args) }] };
        
        case 'helcurt_get_events':
          return { content: [{ type: 'text', text: await getEvents(args) }] };
        
        default:
          throw new Error(`Unknown tool: ${name}`);
      }
    } catch (error: any) {
      return {
        content: [{ type: 'text', text: JSON.stringify({ error: error.message }, null, 2) }],
        isError: true
      };
    }
  });

  // Start server
  const transport = new StdioServerTransport();
  await server.connect(transport);
  
  console.error('Helcurt MCP Server running...');
  console.error('Monitored folders:', config.monitoredFolders);
}

main().catch((error) => {
  console.error('Fatal error:', error);
  process.exit(1);
});
