# 🎓 Meir Downloader Desktop - Build Instructions

**Convert the desktop app to a Windows EXE file**

## Prerequisites

- Windows 10/11
- Python 3.8+
- Git (optional)

## Step 1: Install Dependencies

```bash
# Navigate to project directory
cd meir-downloader

# Create/activate virtual environment (if not already done)
python -m venv .venv
.venv\Scripts\activate  # Windows Command Prompt
# or
.venv\Scripts\Activate.ps1  # Windows PowerShell

# Install required packages
pip install -r requirements-desktop.txt
```

## Step 2: Test the Desktop App

```bash
# Run the desktop app
python meir_downloader_desktop.py
```

The application should open. Test that:
- ✅ Rabbis load in dropdown
- ✅ Topics load after selecting rabbi
- ✅ Lessons display correctly
- ✅ Downloads work

## Step 3: Build EXE File

```bash
# Build with PyInstaller
pyinstaller --onefile --windowed --name "Meir_Downloader" --icon=icon.ico meir_downloader_desktop.py
```

**Output**: The EXE file will be created at:
```
dist\Meir_Downloader.exe
```

## Step 4: Create Desktop Shortcut

1. Right-click on `dist\Meir_Downloader.exe`
2. Click "Create shortcut"
3. Move shortcut to Desktop

## Running the Application

**Option 1: Direct EXE**
```
Double-click: dist\Meir_Downloader.exe
```

**Option 2: Desktop Shortcut**
```
Double-click the shortcut on Desktop
```

## Features

✅ Modern PyQt6 interface  
✅ Real-time download progress  
✅ Cancel downloads  
✅ Delete downloads  
✅ Search/filter lessons  
✅ Open downloads folder  
✅ Offline operation (after first load)  

## Troubleshooting

### "Python not found" error
- Add Python to PATH during installation
- Or use full path: `C:\Python312\python.exe meir_downloader_desktop.py`

### "Module not found" error
- Ensure virtual environment is activated
- Reinstall requirements: `pip install -r requirements-desktop.txt`

### EXE won't run
- Copy `meir_downloader_desktop.py` to `dist/` folder
- Run from dist directory
- Check Windows Defender antivirus (may block execution)

## Advanced: Create Installer

To create a proper installer (MSI):

```bash
pip install pyinstaller cx_Freeze
cx_Freeze meir_downloader_desktop.py --target-dir dist-installer
```

## Notes

- First run may be slow as PyInstaller extracts DLLs
- Downloads saved to: `C:\Users\<YourUsername>\מוריד שיעורים\`
- All data cached locally after first load
- No server required to run

---

**Size**: ~150 MB (includes Python runtime)  
**Speed**: Fast startup after first run (cached data)  
**Dependencies**: None (all bundled in EXE)

For questions or issues, check the application's status bar for error messages.
