@echo off
REM Build script for Meir Downloader Desktop EXE
REM This script creates a Windows executable from the Python application

echo.
echo ========================================
echo   Meir Downloader - Build Script
echo ========================================
echo.

REM Check if Python is installed
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python is not installed or not in PATH
    echo Please install Python from https://www.python.org/
    pause
    exit /b 1
)

REM Check if virtual environment exists
if not exist ".venv" (
    echo [INFO] Creating virtual environment...
    python -m venv .venv
)

REM Activate virtual environment
echo [INFO] Activating virtual environment...
call .venv\Scripts\activate.bat

REM Install dependencies
echo [INFO] Installing dependencies...
pip install -q -r requirements-desktop.txt

if errorlevel 1 (
    echo [ERROR] Failed to install dependencies
    pause
    exit /b 1
)

REM Create icon file (simple blue square)
echo [INFO] Creating application icon...
python -c "
from PIL import Image, ImageDraw
img = Image.new('RGB', (256, 256), color=(102, 126, 234))
draw = ImageDraw.Draw(img)
draw.text((80, 110), '🎓', fill=(255, 255, 255))
img.save('icon.ico')
" 2>nul

if not exist "icon.ico" (
    echo [WARNING] Could not create icon, continuing without it...
)

REM Build EXE with PyInstaller
echo [INFO] Building executable...
pyinstaller --onefile --windowed ^
    --name "Meir_Downloader" ^
    --icon=icon.ico ^
    --add-data="requirements-desktop.txt:." ^
    meir_downloader_desktop.py

if errorlevel 1 (
    echo [ERROR] Build failed
    pause
    exit /b 1
)

REM Success message
echo.
echo ========================================
echo   BUILD SUCCESSFUL!
echo ========================================
echo.
echo Your executable is ready:
echo   dist\Meir_Downloader.exe
echo.
echo To run the application:
echo   1. Double-click: dist\Meir_Downloader.exe
echo   2. Or create a Desktop shortcut
echo.
pause
