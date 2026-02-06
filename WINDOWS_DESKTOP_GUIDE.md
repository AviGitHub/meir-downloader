# 🚀 Meir Downloader Desktop - Windows Setup Guide

**Create a Windows EXE that works on any Windows PC**

---

## 📋 Quick Summary

We've created a **modern desktop application** that:
- ✅ Runs on Windows 10/11
- ✅ Downloads lessons from Machon Meir
- ✅ Shows download progress in real-time
- ✅ Can be packaged as a single EXE file
- ✅ Works offline (after first load)
- ✅ Has a beautiful, modern UI

---

## 🛠️ Setup Instructions

### Step 1: Install Python

1. Go to https://www.python.org/downloads/
2. Download **Python 3.10** or newer (64-bit recommended)
3. Run the installer
4. ⚠️ **IMPORTANT**: Check "Add Python to PATH"
5. Click "Install Now"

**Verify installation**:
```cmd
python --version
```

Should show: `Python 3.10.x` or higher

### Step 2: Prepare the Application

1. Open Command Prompt or PowerShell in the `meir-downloader` folder
2. Run:

```cmd
# Create virtual environment
python -m venv .venv

# Activate it (Command Prompt)
.venv\Scripts\activate

# Or activate (PowerShell)
.venv\Scripts\Activate.ps1
```

3. Install dependencies:

```cmd
pip install -r requirements-desktop.txt
```

This will take 2-5 minutes and download ~500MB of packages.

### Step 3: Test the Application

```cmd
python test_desktop.py
```

A window should open with the application. Test:
- ✅ Can select a rabbi from dropdown
- ✅ Topics load after selecting rabbi
- ✅ Lessons appear
- ✅ Can click download button

Close the window when done.

### Step 4: Build the EXE

**Option A: Using Build Script (Easiest)**

```cmd
build.bat
```

This will:
1. Create icon
2. Build the EXE
3. Show success message

**Option B: Manual Build**

```cmd
pyinstaller --onefile --windowed --name "Meir_Downloader" meir_downloader_desktop.py
```

### Step 5: Find Your EXE

The executable will be at:
```
meir-downloader\dist\Meir_Downloader.exe
```

That's it! You can:
- **Double-click** to run
- **Create shortcut** on Desktop
- **Send to others** (no Python required!)

---

## 📦 Distributing the EXE

Once you have `dist\Meir_Downloader.exe`:

### To use on your Windows PC:
1. Copy `dist\Meir_Downloader.exe` to Desktop
2. Double-click to run
3. Or create a shortcut

### To send to others:
1. Zip the `dist\Meir_Downloader.exe` file
2. Send via email or cloud storage
3. They can extract and run (Windows 10/11 required)

### To install on all PCs:
Create an installer:
```cmd
pip install cx_Freeze
cxfreeze meir_downloader_desktop.py --target-dir dist-installer
```

---

## 🎯 Features

### Download Management
- 📊 Real-time progress bars
- ⚠️ Shows download speed
- ❌ Cancel button for each download
- ✅ Success notifications

### Search & Filter
- 🔍 Search by lesson name
- 📋 Filter by rabbi
- 📚 Filter by topic
- 🔄 Reset all filters

### File Organization
Downloads saved to:
```
C:\Users\YourName\מוריד שיעורים\
└── הרב שם\
    └── שם הסדרה\
        └── 001-שם_השיעור.mp3
```

### Easy Access
- 📂 "Open Folder" button to view downloads
- 💾 Downloaded files accessible from File Explorer
- 📱 Can copy files to phone/tablet

---

## ❓ Troubleshooting

### "Python not found"
**Solution**: 
- Reinstall Python and CHECK "Add Python to PATH"
- Or use full path: `C:\Python310\python.exe test_desktop.py`

### "ModuleNotFoundError"
**Solution**:
- Ensure virtual environment is activated
- See `.venv\Scripts\activate` prompt before command
- Reinstall: `pip install -r requirements-desktop.txt`

### EXE won't run
**Solution**:
- Try running from Administrator
- Check Windows Defender (may be blocking)
- Try: `dist\Meir_Downloader.exe` from command line to see error

### App crashes on startup
**Solution**:
- Check internet connection (first run needs API access)
- Try running test first: `python test_desktop.py`
- Look for error in Command Prompt window

### Very slow first run
- Normal! PyInstaller extracts files (~1-2 min)
- Future runs will be fast (data cached)

---

## 📊 File Sizes

- **EXE file**: ~180 MB (includes Python runtime)
- **First run**: Downloads ~500 MB of dependencies to cache
- **Downloaded lessons**: MP3 files only (varies by content)

---

## 🔄 Updating the App

When new features are available:

1. Get updated files
2. Update virtual environment: `pip install -r requirements-desktop.txt`
3. Rebuild: `build.bat`
4. Replace old `dist\Meir_Downloader.exe`

---

## 💡 Tips & Tricks

### Create Desktop Shortcut
1. Right-click `Meir_Downloader.exe`
2. "Create shortcut"
3. Move to Desktop

### Batch Download
1. Select rabbi
2. Select topic
3. Keep clicking download on multiple lessons
4. Watch progress in "Active Downloads" panel

### Organize Downloads
Downloads auto-organize by:
```
Rabbi Name → Series Name → Lesson
```

Very organized and easy to find!

### Share with Others
- Send the `dist\Meir_Downloader.exe` file
- They don't need Python installed
- Works on Windows 10/11 (64-bit)

---

## 🎓 What's Inside

**Technology Stack**:
- **PyQt6** - Beautiful, native Windows UI
- **Python 3.10+** - Robust backend
- **Requests** - Network operations
- **PyInstaller** - EXE packaging

**Architecture**:
- Single-file executable
- No installation required
- All dependencies bundled
- Works offline (after initial load)

---

## 📞 Support

If you encounter issues:

1. Check the **Status Bar** (bottom of window) for error messages
2. Try running `python test_desktop.py` to diagnose
3. Check Command Prompt window for detailed error logs
4. Verify internet connection (first run needs API access)

---

## ✅ Next Steps

1. **Test**: `python test_desktop.py`
2. **Build**: `build.bat`
3. **Run**: `dist\Meir_Downloader.exe`
4. **Share**: Send the EXE to others!

---

**Enjoy downloading lessons! 🎓📚**
