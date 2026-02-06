# 🚀 RUNNING INSTRUCTIONS - Meir Downloader

**Version**: 1.0  
**Last Updated**: February 6, 2026  
**Platform Support**: Linux, macOS, Windows  

---

## 📋 Table of Contents

1. [Quick Start (5 minutes)](#quick-start)
2. [System Requirements](#system-requirements)
3. [Installation Guide](#installation-guide)
4. [Running the Application](#running-the-application)
5. [Using the Web Interface](#using-the-web-interface)
6. [API Reference](#api-reference)
7. [Downloading Lessons](#downloading-lessons)
8. [Troubleshooting](#troubleshooting)
9. [File Structure](#file-structure)

---

## ⚡ Quick Start

Get the app running in **5 minutes**:

### Step 1: Clone & Navigate
```bash
cd /path/to/meir-downloader
```

### Step 2: Create Virtual Environment
```bash
# Linux/macOS
python3 -m venv .venv
source .venv/bin/activate

# Windows
python -m venv .venv
.venv\Scripts\activate
```

### Step 3: Install Dependencies
```bash
pip install Flask==2.3.0 Flask-CORS==4.0.0 requests==2.31.0
```

### Step 4: Start Backend
```bash
# Linux/macOS
.venv/bin/python backend.py

# Windows
.venv\Scripts\python backend.py
```

### Step 5: Open Frontend
Open your browser: **http://localhost:5000**

**Done! ✅** You now have the app running.

---

## 🖥️ System Requirements

### Minimum Requirements
- **Python**: 3.8+ (tested with 3.12.3)
- **RAM**: 512 MB
- **Disk**: 500 MB (for app + lessons)
- **Network**: Internet connection (for downloading)

### Recommended Requirements
- **Python**: 3.10+
- **RAM**: 2 GB
- **Disk**: 5 GB+ (for storing lessons)

### Supported Operating Systems
- ✅ Linux (Ubuntu 18.04+, Debian 10+, Fedora 30+)
- ✅ macOS (10.13+, Intel & Apple Silicon)
- ✅ Windows (10, 11, WSL2)

---

## 📦 Installation Guide

### 1. Prerequisites Setup

#### On Linux
```bash
# Ubuntu/Debian
sudo apt-get update
sudo apt-get install python3 python3-pip python3-venv curl

# Verify Python
python3 --version  # Should be 3.8+
```

#### On macOS
```bash
# Using Homebrew (install from https://brew.sh if needed)
brew install python3 curl

# Verify Python
python3 --version  # Should be 3.8+
```

#### On Windows
1. Download Python 3.10+ from https://www.python.org/downloads/
2. During installation, **CHECK** "Add Python to PATH"
3. Open Command Prompt and verify:
```cmd
python --version
```

### 2. Clone/Download Project

```bash
# Option A: If you have git
git clone https://github.com/yourusername/meir-downloader.git
cd meir-downloader

# Option B: Download manually
# Download ZIP from GitHub and extract
cd meir-downloader
```

### 3. Create Virtual Environment

```bash
# Linux/macOS
python3 -m venv .venv
source .venv/bin/activate

# Windows (Command Prompt)
python -m venv .venv
.venv\Scripts\activate

# Windows (PowerShell)
python -m venv .venv
.venv\Scripts\Activate.ps1
```

**You should see `(.venv)` prefix in your terminal**

### 4. Install Python Dependencies

```bash
# With virtual environment activated
pip install --upgrade pip
pip install Flask==2.3.0 Flask-CORS==4.0.0 requests==2.31.0
```

**Verify installation**:
```bash
pip list
```

Should show:
```
Flask                    2.3.0
Flask-CORS               4.0.0
requests                 2.31.0
Werkzeug                 2.3.0
...
```

### 5. Verify Backend Code

```bash
# Check that backend.py exists and is valid
python -m py_compile backend.py
echo "Backend verified!"
```

### 6. Test Backend Server

```bash
# Start test
python backend.py

# In another terminal (Linux/macOS):
sleep 3 && curl -s http://localhost:5000/health | python -m json.tool

# You should see: {"status": "ok"}
```

---

## 🎯 Running the Application

### Standard Operation

#### Terminal 1: Start Backend Server
```bash
# Ensure virtual environment is activated
source .venv/bin/activate  # Linux/macOS
# or
.venv\Scripts\activate      # Windows

# Start Flask server
python backend.py
```

**Expected output**:
```
 * Serving Flask app 'backend'
 * Debug mode: on
 * Running on http://127.0.0.1:5000
 * Press CTRL+C to quit
```

#### Terminal 2: Open Frontend
Open your web browser and go to:
```
http://localhost:5000
```

**The application is now ready to use!** ✅

### Alternative: Run in Background (Linux/macOS)

```bash
# Start backend in background
.venv/bin/python backend.py > server.log 2>&1 &

# Get process ID
echo $! > .server.pid

# Check if running
ps aux | grep backend.py

# Stop server later
kill $(cat .server.pid)
```

### Alternative: Run with Different Port

```bash
# If port 5000 is busy, use port 8080
FLASK_PORT=8080 python backend.py

# Then access: http://localhost:8080
```

---

## 💻 Using the Web Interface

### User Interface Tour

The application has a clean interface with **Hebrew right-to-left (RTL)** support.

### Step-by-Step Guide

#### 1. Select Rabbi
```
┌─────────────────────────────────────────┐
│ בחר רב (Choose Rabbi)              ▼  │
│ ┌─────────────────────────────────────┐ │
│ │ הרב אורי עמוס שרקי       (203)    │ │
│ │ הרב חנוך בן פזי זצל      (343)    │ │
│ │ הרב ערן טמיר               (71)    │ │
│ │ הרב מרדכי ענתבי           (30)    │ │
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```
**Action**: Click dropdown to select a rabbi  
**Result**: Series and topics for that rabbi load

#### 2. Select Series (Optional)
```
┌─────────────────────────────────────────┐
│ בחר סדרה (Choose Series)           ▼  │
│ ┌─────────────────────────────────────┐ │
│ │ באור התפילה ע"פ עולת ראיה (203)   │ │
│ │ (Other series when available)      │ │
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```
**Action**: Optional - further narrow down lessons  
**Result**: Topics filter updates

#### 3. Select Subject/Torah Portion (Optional)
```
┌─────────────────────────────────────────┐
│ בחר פרשה (Choose Subject)          ▼  │
│ ┌─────────────────────────────────────┐ │
│ │ פרשת שלח לך                        │ │
│ │ פרשת בא                            │ │
│ │ פרשת בשלח                          │ │
│ │ פרשת עקב                           │ │
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```
**Action**: Optional - filter by Torah portion  
**Result**: Lessons matching this portion filter

#### 4. Select Topic (Recommended)
```
┌─────────────────────────────────────────┐
│ בחר נושא (Choose Topic)             ▼  │
│ ┌─────────────────────────────────────┐ │
│ │ נפש החיים              (74)       │ │
│ │ עולת ראיה               (203)      │ │
│ │ כוזרי                 (248)      │ │
│ │ אורות הקודש             (268)      │ │
│ │ (60+ topics available)            │ │
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```
**Action**: Select specific topic of interest  
**Result**: Lessons filter by topic

#### 5. Select Occasion/Holiday (Optional)
```
┌─────────────────────────────────────────┐
│ בחר אירוע (Choose Occasion)        ▼  │
│ ┌─────────────────────────────────────┐ │
│ │ פסח (Passover)                     │ │
│ │ חנוכה (Hanukkah)                   │ │
│ │ (Holiday-specific lessons)        │ │
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```
**Action**: Optional - filter by holiday/occasion  
**Result**: Holiday-specific lessons displayed

#### 6. Browse Lessons
```
┌────────────────────────────────────────────────────────────┐
│ תוצאות (Results)                                           │
├────────────────────────────────────────────────────────────┤
│ ┌──────────────────────────────────────────────────────┐   │
│ │ בְּרָכָה – סיפור קצר לטז'ו בשבט                      │   │
│ │ הרב אמיר דומן | לחיות ולעבוד אמונה | שיעור 144    │   │
│ │ 28/01/2026 | משך: 45 דקות                          │   │
│ │                                     [⬇️ Download]   │   │
│ └──────────────────────────────────────────────────────┘   │
│                                                            │
│ ┌──────────────────────────────────────────────────────┐   │
│ │ מעשיות ודברים טובים                                │   │
│ │ הרב אורי עמוס שרקי | קדוש לה' | שיעור 23          │   │
│ │ 21/01/2026 | משך: 52 דקות                          │   │
│ │                                     [⬇️ Download]   │   │
│ └──────────────────────────────────────────────────────┘   │
│ [Previous] ... Page 1 of 5 ... [Next]                      │
└────────────────────────────────────────────────────────────┘
```

#### 7. Download Lesson
**Action**: Click "⬇️ Download" button on any lesson

**Expected behavior**:
- Button shows loading state
- File downloads to your Downloads folder
- File saved to: `~/meir-downloader/<rabbi>/<series>/<number>-<name>.mp3`
- Button shows ✅ when complete

#### 8. Reset Filters
**Action**: Click "🔄 Reset" button

**Result**: All filters reset to default state, start over

---

## 🔌 API Reference

### Base URL
```
http://localhost:5000
```

### Authentication
No authentication required (local use)

### Response Format
All responses are JSON

### Endpoints

#### 1. Get All Rabbis
```
GET /api/rabbis
```

**Response**:
```json
{
  "rabbis": [
    {
      "id": "3774",
      "name": "הרב אורי עמוס שרקי",
      "count": 203
    }
  ]
}
```

#### 2. Get Series
```
GET /api/series?rabbi_id=3774
```

**Parameters**:
- `rabbi_id` (optional): Filter by rabbi

**Response**:
```json
{
  "series": [
    {
      "id": "22542",
      "name": "באור התפילה עפ\"פ עולת ראי\"ה - התשעד",
      "count": 203
    }
  ]
}
```

#### 3. Get Torah Portions
```
GET /api/subjects?rabbi_id=3774&series_id=22542
```

**Parameters**:
- `rabbi_id` (optional)
- `series_id` (optional)

**Response**:
```json
{
  "subjects": [
    {
      "id": "127",
      "name": "פרשת שלח לך",
      "count": 1
    }
  ]
}
```

#### 4. Get Topics
```
GET /api/topics?rabbi_id=3774&subject_id=127
```

**Parameters**:
- `rabbi_id` (optional)
- `subject_id` (optional)

**Response**:
```json
{
  "topics": [
    {
      "id": "3914",
      "name": "נפש החיים",
      "count": 74
    }
  ]
}
```

#### 5. Get Occasions
```
GET /api/occasions?topic_id=3914
```

**Parameters**:
- `topic_id` (optional)

**Response**:
```json
{
  "occasions": [
    {
      "id": "3754",
      "name": "פסח",
      "count": 2
    }
  ]
}
```

#### 6. Get Lessons
```
GET /api/lessons?rabbi_id=3774&topic_id=3914&page=1
```

**Parameters**:
- `rabbi_id` (optional)
- `series_id` (optional)
- `subject_id` (optional)
- `topic_id` (optional)
- `occasion_id` (optional)
- `page` (default: 1)

**Response**:
```json
{
  "lessons": [
    {
      "id": "373379",
      "post_id": "373379",
      "rabbi_name": "הרב אורי עמוס שרקי",
      "series_name": "באור התפילה",
      "chapter": 144,
      "date": "28/01/2026",
      "duration": 45,
      "name": "בְּרָכָה – סיפור קצר לטז'ו בשבט"
    }
  ],
  "page": 1
}
```

#### 7. Download Lesson
```
POST /api/download
Content-Type: application/json

{
  "lesson_id": "373379",
  "lesson_name": "בְּרָכָה – סיפור קצר",
  "rabbi_name": "הרב אורי עמוס שרקי",
  "series_name": "באור התפילה",
  "chapter": 144
}
```

**Response** (Success):
```json
{
  "success": true,
  "filepath": "/home/user/meir-downloader/הרב אורי עמוס שרקי/באור התפילה/144-בְּרָכָה.mp3",
  "filename": "144-בְּרָכָה.mp3"
}
```

#### 8. Health Check
```
GET /health
```

**Response**:
```json
{
  "status": "ok"
}
```

---

## 📥 Downloading Lessons

### Automatic Organization

When you download a lesson, files are saved with this structure:

```
~/meir-downloader/
├── הרב אורי עמוס שרקי/
│   ├── באור התפילה/
│   │   ├── 001-שיעור_ראשון.mp3
│   │   ├── 002-שיעור_שני.mp3
│   │   └── 003-שיעור_שלישי.mp3
│   └── קדוש_לה'/
│       └── 024-נושא_חדש.mp3
├── הרב חנוך בן פזי/
│   └── לחקור_את_התורה/
│       ├── 101-מבוא.mp3
│       └── 102-פרק_ראשון.mp3
└── הרב ערן טמיר/
    └── שיעורים_מיוחדים/
        └── 050-דרוש_יחודי.mp3
```

### Download Location

**Default location**: `~/meir-downloader/`

To change location, edit `backend.py` line 15:
```python
DOWNLOAD_PATH = os.path.expanduser("~/meir-downloader")  # Change this path
```

### Batch Downloading

Use the API directly with curl:

```bash
# Get all lessons for a rabbi
curl -s "http://localhost:5000/api/lessons?rabbi_id=3774&page=1" | \
  python3 -c "
import sys, json
data = json.load(sys.stdin)
for lesson in data['lessons']:
    print(f\"{lesson['id']},{lesson['name']},{lesson['rabbi_name']},{lesson['series_name']}\")
" > lessons.csv

# Then process with a script
```

---

## 🐛 Troubleshooting

### Issue: Port 5000 Already in Use

**Error**: `OSError: [Errno 48] Address already in use`

**Solution**:
```bash
# Find process using port 5000
lsof -i :5000  # Linux/macOS
netstat -ano | findstr :5000  # Windows

# Kill the process
kill -9 <PID>  # Linux/macOS
taskkill /PID <PID> /F  # Windows

# Or use different port
FLASK_PORT=8080 python backend.py
```

### Issue: Virtual Environment Not Activating

**Error**: `command not found: python` or `ModuleNotFoundError`

**Solution**:
```bash
# Verify venv exists
ls -la .venv  # Linux/macOS
dir .venv     # Windows

# Recreate venv if needed
rm -rf .venv  # Linux/macOS
rmdir .venv   # Windows
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

### Issue: Flask Module Not Found

**Error**: `ModuleNotFoundError: No module named 'flask'`

**Solution**:
```bash
# Ensure venv is activated (you should see (.venv) prompt)
source .venv/bin/activate

# Reinstall packages
pip install Flask==2.3.0 Flask-CORS==4.0.0 requests==2.31.0

# Verify
pip list | grep Flask
```

### Issue: Hebrew Text Shows as Gibberish

**Error**: Characters like `???` or `□□□` instead of Hebrew

**Solution**:
```bash
# Usually works automatically, but if not:
# 1. Check browser encoding: Ctrl+Shift+R (reload)
# 2. Update backend.py to force UTF-8
# Add to top of backend.py:
import sys
sys.stdout.reconfigure(encoding='utf-8')
```

### Issue: Backend Crashes with Large Downloads

**Error**: `MemoryError` or process dies during download

**Solution**:
```bash
# Check available disk space
df -h  # Linux/macOS
dir C:  # Windows

# Use background download (future feature)
# For now, download lessons in smaller batches
```

### Issue: Lessons Won't Download

**Error**: Download button shows error, file not created

**Solution**:
```bash
# 1. Check backend logs
# Look for error messages in terminal where Flask is running

# 2. Verify download path exists
mkdir -p ~/meir-downloader

# 3. Check file permissions
ls -la ~/meir-downloader

# 4. Test API manually
curl -X POST http://localhost:5000/api/download \
  -H "Content-Type: application/json" \
  -d '{"lesson_id":"373379","lesson_name":"Test","rabbi_name":"Test","series_name":"Test","chapter":1}'
```

### Issue: Cannot Connect to meirtv.com

**Error**: API returns `401` or `400 Bad Request`

**Solution**:
```bash
# 1. Check internet connection
ping -c 3 meirtv.com  # Linux/macOS
ping meirtv.com       # Windows

# 2. Try direct URL
curl -I "https://meirtv.com"

# 3. Check firewall/proxy settings
# May need to disable corporate proxy

# 4. Use sample data for now (automatically used as fallback)
```

### Issue: Can't Open http://localhost:5000

**Error**: `Connection refused` or `This site can't be reached`

**Solution**:
```bash
# 1. Verify backend is running
ps aux | grep backend.py

# 2. Check if port 5000 is listening
netstat -tulpn | grep 5000  # Linux/macOS

# 3. Try different browser
# Sometimes browser cache issues - try Incognito

# 4. Try localhost directly
curl -s http://localhost:5000/health

# 5. Check firewall
# May need to allow Flask in firewall settings
```

---

## 📁 File Structure

```
meir-downloader/
├── backend.py                    # Flask API server
├── index.html                    # React frontend (all-in-one)
├── requirements.txt              # Python dependencies
├── README.md                     # Project overview
├── QUICKSTART.md                 # Quick setup guide
├── RUNNING_INSTRUCTIONS.md       # This file
├── FILTERS_GUIDE.md              # Filter documentation
├── IMPLEMENTATION_LOG.md         # Implementation details
├── PROJECT_STATUS.md             # Project status
├── TEST_RESULTS.md               # Test results
├── DATA_INVENTORY.md             # Complete data listing
├── KNOWLEDGE_BASE.md             # API research
├── .venv/                        # Virtual environment (created)
│   ├── bin/                      # Executables (Linux/macOS)
│   │   └── python
│   ├── Scripts/                  # Executables (Windows)
│   │   └── python.exe
│   └── lib/                      # Installed packages
├── sample_data/                  # Sample JSON files
│   ├── subjects.json
│   ├── parashot.json
│   ├── series.json
│   ├── rabies.json
│   └── fetch-response.json
└── meir-downloader/              # Downloaded lessons (created at runtime)
    ├── הרב אורי עמוס שרקי/
    ├── הרב חנוך בן פזי/
    └── [other rabbis]/
```

---

## 🎓 Common Use Cases

### Use Case 1: Download All Lessons by a Rabbi
```bash
# Manually:
# 1. Select rabbi from dropdown
# 2. Click "Reset Filters" 
# 3. Scroll and click Download on each lesson

# Programmatically:
for topic_id in {3914,3705,3780,3787,3804,3760}; do
  curl "http://localhost:5000/api/lessons?rabbi_id=3774&topic_id=$topic_id&page=1"
done
```

### Use Case 2: Download Holiday-Specific Lessons
```bash
# UI:
# 1. Select Rabbi
# 2. Select Occasion: "פסח" (Passover)
# 3. Download all Passover lessons

# API:
curl "http://localhost:5000/api/lessons?occasion_id=3754&page=1"
```

### Use Case 3: Download Specific Torah Portion Lessons
```bash
# UI:
# 1. Select Rabbi
# 2. Select Subject: "פרשת שלח לך"
# 3. Download all lessons for that Torah portion

# API:
curl "http://localhost:5000/api/lessons?subject_id=127&page=1"
```

### Use Case 4: Explore Topics by Rabbi
```bash
# UI:
# 1. Select Rabbi
# 2. Browse Topics dropdown to see available topics
# 3. Select topic to see related lessons

# API:
curl "http://localhost:5000/api/topics?rabbi_id=3774" | python -m json.tool
```

---

## 📞 Support

For issues or questions:
1. Check [Troubleshooting](#troubleshooting) section
2. Check backend console for error messages
3. Verify all dependencies are installed: `pip list`
4. Check internet connection to meirtv.com
5. Review logs in `backend.py` output

---

## 📝 Version History

- **v1.0** (Feb 6, 2026): Initial release
  - 5-level hierarchical filters
  - 9 API endpoints
  - React UI with Hebrew support
  - Automatic file organization

---

## ✅ Checklist Before Running

- [ ] Python 3.8+ installed
- [ ] Virtual environment created
- [ ] Dependencies installed (`pip install -r requirements.txt`)
- [ ] `backend.py` and `index.html` present
- [ ] Port 5000 is available
- [ ] Internet connection (for downloading)
- [ ] At least 500 MB free disk space

---

**Happy downloading! 🎉**

For more information, see [TEST_RESULTS.md](TEST_RESULTS.md) and [PROJECT_STATUS.md](PROJECT_STATUS.md).
