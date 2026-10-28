/**
 * Build script for creating Helcurt standalone executable
 */

import { execSync } from 'child_process';
import fs from 'fs/promises';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

async function build() {
  console.log('🔨 Building Helcurt MCP Server executable...\n');

  try {
    // Step 1: Compile TypeScript
    console.log('📦 Compiling TypeScript...');
    execSync('tsc', { stdio: 'inherit' });
    console.log('✅ TypeScript compiled\n');

    // Step 2: Create executable with pkg fork
    console.log('🎯 Creating executable...');
    execSync('npx @yao-pkg/pkg . --output helcurt-server.exe --targets node18-win-x64 --compress GZip', { 
      stdio: 'inherit' 
    });
    console.log('✅ Executable created: helcurt-server.exe\n');

    // Step 3: Copy configuration files
    console.log('📄 Copying configuration files...');
    await fs.copyFile(
      path.join(__dirname, 'helcurt-config.example.json'),
      path.join(__dirname, 'dist', 'helcurt-config.example.json')
    );
    await fs.copyFile(
      path.join(__dirname, 'CONFIG.md'),
      path.join(__dirname, 'dist', 'CONFIG.md')
    );
    console.log('✅ Configuration files copied\n');

    console.log('✨ Build complete!');
    console.log('\nDeployment files:');
    console.log('  - helcurt-server.exe        (Main executable)');
    console.log('  - helcurt-config.example.json (Example configuration)');
    console.log('  - CONFIG.md                 (Configuration guide)');
    console.log('\nTo deploy:');
    console.log('  1. Copy helcurt-server.exe to target machine');
    console.log('  2. Create helcurt-config.json from example');
    console.log('  3. Configure monitored folders');
    console.log('  4. Add to VS Code MCP settings');

  } catch (error) {
    console.error('❌ Build failed:', error.message);
    process.exit(1);
  }
}

build();
