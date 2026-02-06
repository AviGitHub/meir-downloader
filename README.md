# 📚 Meir Downloader - מוריד שיעורים

A modern Python application with React UI for downloading lessons (שיעורים) from Machon Meir (מכון מאיר).

## ✨ Features

- 🎓 Browse lessons by rabbi and series
- 📥 Download lessons with one click
- 🌐 Beautiful Hebrew-friendly web interface
- 📁 Automatic organization: `rabbi/series/chapter-name.mp3`
- 🔍 Filter by rabbi, series, and more
- ⚡ Fast and responsive UI

## 📋 Requirements

- Python 3.8 or higher
- Modern web browser (Chrome, Firefox, Safari, Edge)
- Internet connection

## 🚀 Quick Start

### 1. Clone or Download the Project

```bash
cd /home/azadok/projects/meir-downloader
```

### 2. Run the Application

```bash
# On Linux/Mac
chmod +x run.sh
./run.sh

# On Windows (if you have Python installed)
pip install -r requirements.txt
python backend.py
```

### 3. Open in Browser

Once the server starts, open your browser to:
```
http://localhost:5000/index.html
```

## 📖 Usage

1. **Select a Rabbi** - Choose from the list of rabbis (הרב...)
2. **Select a Series** (optional) - Filter by lesson series
3. **Browse Lessons** - Scroll through available lessons
4. **Download** - Click the download button (📥) to save the lesson

## 📁 Download Location

By default, lessons are saved to:
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

- **Backend**: Flask (Python web framework)
- **Frontend**: React (with Babel transpiler, no build needed)
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
├── backend.py           # Python Flask server
├── index.html          # React UI (all-in-one HTML file)
├── requirements.txt    # Python dependencies
├── run.sh             # Startup script
├── KNOWLEDGE_BASE.md  # API documentation
└── README.md          # This file
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

## 🐛 Troubleshooting

### "Failed to connect to server"
- Make sure the backend is running: `python backend.py`
- Check if port 5000 is already in use
- Try a different port (see Configuration section)

### "Audio not found"
- The lesson might not have audio available on the website
- Try a different lesson

### "Download failed"
- Check your internet connection
- Ensure the meirtv.com website is accessible
- Try downloading again

## 📦 Dependencies

All dependencies are in `requirements.txt`:
- **Flask** - Web framework
- **Flask-CORS** - Cross-Origin Resource Sharing
- **requests** - HTTP library for API calls

## 🔐 Privacy & Security

- No data is sent to external servers (except meirtv.com for lessons)
- All lessons are stored locally on your computer
- No tracking or analytics

## 📄 License

This project is provided as-is for personal use.

## 🤝 Contributing

Found a bug or have a suggestion? Feel free to improve this project!

## 📚 Additional Resources

- [Machon Meir Website](https://meirtv.com/)
- [Flask Documentation](https://flask.palletsprojects.com/)
- [React Documentation](https://react.dev/)

---

**Enjoy learning with Machon Meir! 🎓**
