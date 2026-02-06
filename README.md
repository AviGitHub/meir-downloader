# 📚 Meir Downloader - מוריד שיעורים

[![Python 3.8+](https://img.shields.io/badge/python-3.8%2B-blue)](https://www.python.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Platform: Windows/Linux/Mac](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20Mac-lightgrey)](README.md)

A modern Python desktop application for downloading lessons (שיעורים) from **Machon Meir** (מכון מאיר).

**Available as a Windows Desktop App (.exe) or Python application**

## ✨ Features

### 🖥️ Desktop Application (PyQt6)
- 📊 Modern, native Windows UI with Hebrew RTL support
- 🎓 Browse 6+ rabbis with 60+ topics and 650+ lessons
- 📥 Download with real-time progress indicators
- ⚠️ Cancel downloads at any time
- 📂 Auto-organized: `C:\Users\YourName\מוריד שיעורים\<Rabbi>\<Series>\<Lesson>.mp3`
- 🔍 Search and filter functionality
- 💾 Packaged as single Windows .exe (no Python required)

### 🌐 Web Application (Flask + HTML/JS)
- 🎓 Browse lessons by rabbi and series
- 📥 Download lessons with one click
- 📁 Automatic organization by rabbi/series
- 🔍 Filter by multiple criteria
- ⚡ Fast and responsive UI

## 📋 Requirements

### For Desktop App (Recommended)
- **Windows 10/11** (64-bit)
- No Python needed! (.exe is standalone)

### For Python Version
- Python 3.8 or higher
- Modern web browser (Chrome, Firefox, Safari, Edge)
- Internet connection

## 🚀 Quick Start

### Option 1: Windows Desktop App (Easiest) 🖥️

1. **Install Python** (if not already installed)
   - Download from https://www.python.org/downloads/
   - Check "Add Python to PATH"

2. **Setup & Build**
   ```cmd
   pip install -r requirements-desktop.txt
   build.bat
   ```

3. **Run**
   - Double-click `dist\Meir_Downloader.exe`
   - Or share the .exe file with others!

📖 **Full guide**: See [WINDOWS_DESKTOP_GUIDE.md](WINDOWS_DESKTOP_GUIDE.md)

### Option 2: Web Application

1. **Install Dependencies**
   ```bash
   pip install -r requirements.txt
   ```

2. **Run the Server**
   ```bash
   python backend.py
   ```

3. **Open in Browser**
   ```
   http://localhost:5000
   ```

📖 **Full guide**: See [RUNNING_INSTRUCTIONS.md](RUNNING_INSTRUCTIONS.md)

## 📖 Usage

### Desktop App
1. **Select Rabbi** - Choose from dropdown (6 rabbis)
2. **Select Topic** - Filter by topic (60+ available)
3. **Search** - Find lessons by name
4. **Download** - Click download button with progress tracking
5. **Manage** - Cancel downloads or open folder

### Web App
1. **Select a Rabbi** - Choose from the list of rabbis (הרב...)
2. **Select a Series** (optional) - Filter by lesson series
3. **Browse Lessons** - Scroll through available lessons
4. **Download** - Click the download button (📥) to save the lesson

## 📁 Download Location

### Desktop App
```
C:\Users\YourName\מוריד שיעורים\
└── הרב שם\
    └── שם הסדרה\
        └── 001-שם_השיעור.mp3
```

### Web App
```
~/meir-downloader/
```

Directory structure:
```
meir-downloader/
├── הרב אורי שרקי/
│   ├── הלכה יומית/
│   │   ├── 001-שם היום.mp3
│   │   ├── 002-שם היום.mp3
│   │   └── ...
│   └── סדרה אחרת/
├── הרב דב ביגון/
│   └── ...
```

## 🛠️ Technical Stack

### Desktop Application
- **Framework**: PyQt6 (cross-platform GUI)
- **Packager**: PyInstaller (creates standalone .exe)
- **Language**: Python 3.8+
- **Threading**: QThread for non-blocking downloads

### Web Application
- **Backend**: Flask (Python web framework)
- **Frontend**: HTML/JavaScript
- **API Source**: Machon Meir website (meirtv.com)
- **Data Format**: JSON + HTML parsing

## 🔧 API Endpoints

### Available Endpoints

- `GET /api/rabbis` - Get all available rabbis
- `GET /api/series?rabbi_id=ID` - Get series for a rabbi
- `GET /api/lessons?rabbi_id=ID&series_id=ID&page=PAGE` - Get lessons
- `POST /api/download` - Download a lesson
- `GET /api/config` - Get app configuration
- `GET /health` - Health check

### Example

```bash
# Get rabbis
curl http://localhost:5000/api/rabbis

# Get series for rabbi ID 12345
curl "http://localhost:5000/api/series?rabbi_id=12345"

# Get lessons
curl "http://localhost:5000/api/lessons?page=1"
```

## 🎨 UI Features

- **Real-time Filtering** - See results as you select filters
- **Pagination** - Navigate through lesson pages
- **Download Progress** - Visual feedback during download
- **Responsive Design** - Works on desktop and mobile
- **Hebrew Support** - Full RTL (right-to-left) layout

## 📝 File Structure

```
meir-downloader/
├── 🖥️ Desktop App
│   ├── meir_downloader_desktop.py  # Main PyQt6 app (470 lines)
│   ├── requirements-desktop.txt    # Desktop dependencies
│   ├── build.bat                   # Auto-build script for Windows
│   ├── test_desktop.py            # Test script before building
│   └── BUILD_DESKTOP_EXE.md       # Build instructions
│
├── 🌐 Web App
│   ├── backend.py                 # Flask API server
│   ├── index.html                 # Web UI
│   ├── requirements.txt           # Web dependencies
│   └── RUNNING_INSTRUCTIONS.md    # Setup guide
│
├── 📖 Documentation
│   ├── README.md                  # This file
│   ├── WINDOWS_DESKTOP_GUIDE.md   # Desktop app guide
│   ├── test_api.py               # API test suite
│   └── .gitignore
```

## ⚙️ Configuration

### Change Download Directory

Edit `backend.py` line 14:
```python
DEFAULT_DOWNLOAD_PATH = Path.home() / "meir-downloader"  # Change this path
```

### Change Server Port

Edit the last line of `backend.py`:
```python
app.run(debug=True, port=5000)  # Change 5000 to your preferred port
```

Then update the API URL in `index.html`:
```javascript
const API_URL = 'http://localhost:5000/api';  // Update port here
```

## � Available Content

**6 Rabbis** with lessons:
- הרב אורי שרקי
- הרב דב ביגון
- הרב אברהם יצחק הכהן קוק
- And more...

**60+ Topics** including:
- הלכה יומית (Daily Halacha)
- דברי תורה (Torah Insights)
- עברית לישראלים (Hebrew for Israelis)
- And many more...

**650+ Lessons** ready to download

## 🌍 API Endpoints (Web App Only)

- `GET /api/rabbis` - Get all available rabbis
- `GET /api/series?rabbi_id=ID` - Get series for a rabbi
- `GET /api/lessons?rabbi_id=ID&series_id=ID&page=PAGE` - Get lessons
- `POST /api/download` - Download a lesson
- `GET /api/config` - Get app configuration

## 🐛 Troubleshooting

### Desktop App Issues

**App won't start:**
- Check Windows version (need Windows 10/11)
- Run from Command Prompt to see error messages
- Try right-click → Run as Administrator

**Downloads not working:**
- Check internet connection
- Verify meirtv.com is accessible
- Try a different lesson

### Web App Issues

**"Failed to connect to server"**
- Make sure the backend is running: `python backend.py`
- Check if port 5000 is already in use
- Try a different port (see Configuration section)

**"Audio not found"**
- The lesson might not have audio available on the website
- Try a different lesson

**"Download failed"**
- Check your internet connection
- Ensure the meirtv.com website is accessible
- Try downloading again

## 📦 Dependencies

### Desktop App
- **PyQt6** - Modern GUI framework
- **requests** - HTTP library
- **PyInstaller** - Create .exe files

### Web App
- **Flask** - Web framework
- **Flask-CORS** - Cross-Origin Resource Sharing
- **requests** - HTTP library

All dependencies are in `requirements.txt` and `requirements-desktop.txt`.

## 💻 System Requirements

| Platform | Minimum | Recommended |
|----------|---------|-------------|
| **Windows** | Windows 10 | Windows 10/11 (64-bit) |
| **Python** | 3.8 | 3.10+ |
| **RAM** | 512 MB | 2+ GB |
| **Storage** | 200 MB | 1+ GB (for lessons) |

## 🔐 Privacy & Security

- ✅ No data is sent to external servers (except meirtv.com for lessons)
- ✅ All lessons are stored locally on your computer
- ✅ No tracking or analytics
- ✅ Open source - inspect the code yourself

## 📄 License

This project is provided as-is for personal use. See LICENSE file for details.

## 🤝 Contributing

Found a bug? Have an improvement? 
- Open an issue on GitHub
- Submit a pull request

## 📞 Support

For issues or questions:
1. Check the [WINDOWS_DESKTOP_GUIDE.md](WINDOWS_DESKTOP_GUIDE.md)
2. Check the [RUNNING_INSTRUCTIONS.md](RUNNING_INSTRUCTIONS.md)
3. Run `python test_desktop.py` or `python test_api.py` to diagnose
4. Open an issue on GitHub

## 🎓 About Machon Meir

[Machon Meir](https://www.meirtv.com) is an Israeli yeshiva providing online Torah lessons. This tool helps download their content for offline learning.

---

**Made with ❤️ for Torah learning**

## 🤝 Contributing

Found a bug or have a suggestion? Feel free to improve this project!

## 📚 Additional Resources

- [Machon Meir Website](https://meirtv.com/)
- [Flask Documentation](https://flask.palletsprojects.com/)
- [React Documentation](https://react.dev/)

---

**Enjoy learning with Machon Meir! 🎓**
