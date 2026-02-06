#!/usr/bin/env python3
"""
Quick test script for desktop application
Run this to verify the app works before building EXE
"""

import sys
import subprocess
from pathlib import Path

def main():
    print("\n" + "="*50)
    print("  Meir Downloader Desktop - Quick Test")
    print("="*50 + "\n")
    
    # Check Python
    print("[1/4] Checking Python installation...")
    result = subprocess.run([sys.executable, "--version"], capture_output=True, text=True)
    print(f"     ✅ {result.stdout.strip()}")
    
    # Check PyQt6
    print("[2/4] Checking PyQt6 installation...")
    try:
        import PyQt6
        print("     ✅ PyQt6 is installed")
    except ImportError:
        print("     ❌ PyQt6 not installed. Run: pip install -r requirements-desktop.txt")
        return 1
    
    # Check requests
    print("[3/4] Checking requests library...")
    try:
        import requests
        print("     ✅ requests is installed")
    except ImportError:
        print("     ❌ requests not installed. Run: pip install -r requirements-desktop.txt")
        return 1
    
    # Run app
    print("[4/4] Starting application...")
    print("     This will open the desktop app window...")
    print("\n")
    
    try:
        from meir_downloader_desktop import main as app_main
        app_main()
    except Exception as e:
        print(f"     ❌ Error: {str(e)}")
        return 1
    
    return 0

if __name__ == "__main__":
    sys.exit(main())
