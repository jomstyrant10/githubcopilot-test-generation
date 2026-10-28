#!/usr/bin/env python3
"""
Bug Report Log Extraction Tool
Extracts relevant log files based on bug type for easier bug reporting.
"""

import os
import sys
import json
import shutil
import zipfile
import platform
from datetime import datetime
from pathlib import Path
from typing import List, Dict, Any
import glob
import argparse


class BugLogExtractor:
    def __init__(self, config_path: str = "bug-report-config.json"):
        """Initialize the bug log extractor with configuration."""
        self.script_dir = Path(__file__).parent
        self.config_path = self.script_dir / config_path
        self.config = self._load_config()
        self.workspace_root = self.script_dir.parent
        
    def _load_config(self) -> Dict[str, Any]:
        """Load configuration from JSON file."""
        try:
            with open(self.config_path, 'r', encoding='utf-8') as f:
                return json.load(f)
        except FileNotFoundError:
            print(f"❌ Configuration file not found: {self.config_path}")
            sys.exit(1)
        except json.JSONDecodeError as e:
            print(f"❌ Invalid JSON in configuration file: {e}")
            sys.exit(1)
    
    def _expand_env_path(self, path: str) -> List[Path]:
        """Expand environment variables and wildcards in path."""
        # Expand environment variables
        expanded = os.path.expandvars(path)
        
        # Convert to absolute path if relative
        if not os.path.isabs(expanded):
            expanded = str(self.workspace_root / expanded)
        
        # Handle wildcards
        matches = glob.glob(expanded, recursive=True)
        return [Path(m) for m in matches] if matches else [Path(expanded)]
    
    def _get_file_size_mb(self, file_path: Path) -> float:
        """Get file size in megabytes."""
        try:
            return file_path.stat().st_size / (1024 * 1024)
        except OSError:
            return 0.0
    
    def _matches_pattern(self, file_path: Path, patterns: List[str]) -> bool:
        """Check if file matches any of the include patterns."""
        for pattern in patterns:
            if file_path.match(pattern):
                return True
        return False
    
    def _collect_files(self, bug_type_config: Dict[str, Any]) -> List[Path]:
        """Collect all files matching the bug type configuration."""
        collected_files = []
        max_size_mb = bug_type_config.get('maxFileSizeMB', 10)
        include_patterns = bug_type_config.get('includePatterns', ['*'])
        log_paths = bug_type_config.get('logPaths', [])
        
        for log_path in log_paths:
            expanded_paths = self._expand_env_path(log_path)
            
            for path in expanded_paths:
                if path.is_file():
                    # Single file
                    if self._matches_pattern(path, include_patterns):
                        size_mb = self._get_file_size_mb(path)
                        if size_mb <= max_size_mb:
                            collected_files.append(path)
                        else:
                            print(f"⚠️  Skipping large file: {path} ({size_mb:.2f} MB)")
                
                elif path.is_dir():
                    # Directory - collect matching files
                    for pattern in include_patterns:
                        for file_path in path.rglob(pattern):
                            if file_path.is_file():
                                size_mb = self._get_file_size_mb(file_path)
                                if size_mb <= max_size_mb:
                                    collected_files.append(file_path)
                                else:
                                    print(f"⚠️  Skipping large file: {file_path} ({size_mb:.2f} MB)")
        
        # Remove duplicates while preserving order
        seen = set()
        unique_files = []
        for f in collected_files:
            if f not in seen:
                seen.add(f)
                unique_files.append(f)
        
        return unique_files
    
    def _create_output_directory(self, bug_type: str) -> Path:
        """Create output directory for extracted logs."""
        global_settings = self.config.get('globalSettings', {})
        output_base = self.workspace_root / global_settings.get('outputDirectory', 'BugReports')
        
        if global_settings.get('createTimestampedFolder', True):
            timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
            output_dir = output_base / f"{bug_type}_{timestamp}"
        else:
            output_dir = output_base / bug_type
        
        output_dir.mkdir(parents=True, exist_ok=True)
        return output_dir
    
    def _create_system_info_file(self, output_dir: Path) -> None:
        """Create a file with system information."""
        system_info = {
            "timestamp": datetime.now().isoformat(),
            "platform": platform.platform(),
            "python_version": platform.python_version(),
            "machine": platform.machine(),
            "processor": platform.processor(),
            "node": platform.node(),
            "workspace_root": str(self.workspace_root)
        }
        
        info_file = output_dir / "system_info.json"
        with open(info_file, 'w', encoding='utf-8') as f:
            json.dump(system_info, f, indent=2)
        
        print(f"✅ System info saved to: {info_file.name}")
    
    def _copy_files(self, files: List[Path], output_dir: Path) -> None:
        """Copy collected files to output directory."""
        if not files:
            print("⚠️  No files found to copy.")
            return
        
        logs_dir = output_dir / "logs"
        logs_dir.mkdir(exist_ok=True)
        
        copied_count = 0
        for file_path in files:
            try:
                # Create relative structure in output
                if file_path.is_relative_to(self.workspace_root):
                    rel_path = file_path.relative_to(self.workspace_root)
                    dest = logs_dir / rel_path
                else:
                    # For system files, flatten the structure
                    dest = logs_dir / file_path.name
                
                # Handle name conflicts
                counter = 1
                original_dest = dest
                while dest.exists():
                    dest = original_dest.parent / f"{original_dest.stem}_{counter}{original_dest.suffix}"
                    counter += 1
                
                dest.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(file_path, dest)
                copied_count += 1
                print(f"📄 Copied: {file_path.name}")
                
            except (IOError, OSError) as e:
                print(f"❌ Failed to copy {file_path}: {e}")
        
        print(f"\n✅ Successfully copied {copied_count} file(s)")
    
    def _compress_output(self, output_dir: Path, bug_type: str) -> Path:
        """Compress the output directory into a zip file."""
        zip_name = f"{output_dir.name}.zip"
        zip_path = output_dir.parent / zip_name
        
        with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
            for file_path in output_dir.rglob('*'):
                if file_path.is_file():
                    arcname = file_path.relative_to(output_dir.parent)
                    zipf.write(file_path, arcname)
        
        # Remove the uncompressed directory
        shutil.rmtree(output_dir)
        
        return zip_path
    
    def extract_logs(self, bug_type: str, compress: bool = None) -> None:
        """Extract logs for the specified bug type."""
        bug_types = self.config.get('bugTypes', {})
        
        if bug_type not in bug_types:
            print(f"❌ Unknown bug type: {bug_type}")
            print(f"Available types: {', '.join(bug_types.keys())}")
            sys.exit(1)
        
        bug_config = bug_types[bug_type]
        print(f"\n🔍 Extracting logs for: {bug_type}")
        print(f"📝 Description: {bug_config.get('description', 'N/A')}\n")
        
        # Collect files
        print("📂 Collecting files...")
        files = self._collect_files(bug_config)
        print(f"Found {len(files)} file(s)\n")
        
        # Create output directory
        output_dir = self._create_output_directory(bug_config.get('outputPrefix', bug_type))
        print(f"📁 Output directory: {output_dir}\n")
        
        # Copy files
        self._copy_files(files, output_dir)
        
        # Add system info if enabled
        global_settings = self.config.get('globalSettings', {})
        if global_settings.get('includeSystemInfo', True):
            self._create_system_info_file(output_dir)
        
        # Compress if enabled
        should_compress = compress if compress is not None else global_settings.get('compressOutput', True)
        
        if should_compress:
            print(f"\n📦 Compressing output...")
            zip_path = self._compress_output(output_dir, bug_type)
            print(f"✅ Bug report package created: {zip_path}\n")
            print(f"📤 You can now attach this file to your bug report.")
        else:
            print(f"\n✅ Bug report files extracted to: {output_dir}\n")
    
    def list_bug_types(self) -> None:
        """List all available bug types."""
        bug_types = self.config.get('bugTypes', {})
        
        print("\n📋 Available Bug Types:\n")
        for bug_type, config in bug_types.items():
            description = config.get('description', 'No description')
            print(f"  • {bug_type}")
            print(f"    {description}\n")


def main():
    """Main entry point for the script."""
    parser = argparse.ArgumentParser(
        description="Extract log files for bug reporting",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Extract logs for test generation failure
  python extract-bug-logs.py test-generation-failure
  
  # Extract logs without compression
  python extract-bug-logs.py general --no-compress
  
  # List all available bug types
  python extract-bug-logs.py --list
        """
    )
    
    parser.add_argument(
        'bug_type',
        nargs='?',
        help='Type of bug to extract logs for'
    )
    
    parser.add_argument(
        '--list', '-l',
        action='store_true',
        help='List all available bug types'
    )
    
    parser.add_argument(
        '--no-compress',
        action='store_true',
        help='Do not compress the output (keep as folder)'
    )
    
    parser.add_argument(
        '--config', '-c',
        default='bug-report-config.json',
        help='Path to configuration file (default: bug-report-config.json)'
    )
    
    args = parser.parse_args()
    
    try:
        extractor = BugLogExtractor(args.config)
        
        if args.list:
            extractor.list_bug_types()
        elif args.bug_type:
            extractor.extract_logs(args.bug_type, compress=not args.no_compress)
        else:
            parser.print_help()
            print("\n")
            extractor.list_bug_types()
    
    except KeyboardInterrupt:
        print("\n\n⚠️  Operation cancelled by user.")
        sys.exit(130)
    except Exception as e:
        print(f"\n❌ Error: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()
