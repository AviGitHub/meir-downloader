# מוריד שיעורים - Meir Downloader

Download Torah lessons from [Machon Meir](https://meirtv.com) — browse rabbis, series, and download audio lessons with ease.

## ✨ Features

- **Browse 700+ Rabbis** — sorted by lesson count with incremental loading
- **1,000+ Series** — filtered by selected rabbi, sorted alphabetically
- **47,000+ Lessons** — with parallel downloads (up to 4 simultaneous)
- **Per-lesson Progress Bars** — color-coded status (downloading, completed, error, skipped)
- **Smart Skip** — already-downloaded lessons are detected and skipped automatically
- **Folder Picker** — choose your download directory (default: Music/מוריד שיעורים)
- **Organized Downloads** — `Rabbi Name/Series Name/001-Lesson Title.mp3`
- **Israeli Date Format** — dates displayed as dd.MM.yyyy
- **Local Caching** — LiteDB-based cache for fast startup and offline browsing
- **Resilient Networking** — Polly retry policies with exponential backoff
- **Image Caching** — rabbi images cached locally for instant display
- **Self-contained Installer** — WiX v6 MSI, no .NET runtime required
- **REST API** — ASP.NET Core Web API with Swagger documentation

## 📥 Download

Download the latest installer from [GitHub Releases](https://github.com/AviGitHub/meir-downloader/releases/latest).

### System Requirements
- Windows 10/11 (x64)
- Internet connection

## 🏗️ Architecture

```
MeirDownloader.sln
├── MeirDownloader.Core        # Shared models & services (.NET 9)
│   ├── Models/                # Rabbi, Series, Lesson, DownloadProgress
│   └── Services/              # MeirDownloaderService, LiteDbCacheService, ICacheService
├── MeirDownloader.Desktop     # WPF Desktop app (.NET 9, Windows)
│   ├── ViewModels/            # RabbiViewModel, LessonViewModel
│   ├── Services/              # DownloadManager, ImageCacheService
│   ├── Converters/            # XAML value converters
│   └── Theme/                 # ModernTheme (dark theme resources)
├── MeirDownloader.Api         # ASP.NET Core Web API (.NET 9)
│   └── Controllers/           # Rabbis, Series, Lessons endpoints
└── MeirDownloader.Installer   # WiX v6 MSI installer
```

## 🚀 Quick Start

### Run the Desktop App
```bash
dotnet run --project MeirDownloader.Desktop
```

### Run the API
```bash
dotnet run --project MeirDownloader.Api
# Swagger UI: http://localhost:5000/swagger
```

### API Endpoints
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/rabbis` | List all rabbis |
| GET | `/api/series?rabbiId={id}` | List series (optionally filtered) |
| GET | `/api/lessons?rabbiId={id}&seriesId={id}&page=1` | List lessons with pagination |

## 🔨 Build

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [WiX Toolset](https://wixtoolset.org/) (for installer only)

### Build Solution
```bash
dotnet build MeirDownloader.sln
```

### Build Installer (MSI)
```powershell
powershell -File build-installer.ps1
```

## 📁 Download Directory Structure
```
Music/מוריד שיעורים/
  └── הרב דב ביגון/
      └── ספר אורות הקודש/
          ├── 001-הקדמת הרב הנזיר.mp3
          ├── 002-הקדמה כללית לספר.mp3
          └── ...
```

## 🛠️ Tech Stack

- **.NET 9** — C# 13, latest runtime
- **WPF** — Windows Presentation Foundation (Desktop UI)
- **ASP.NET Core** — Web API with Swagger/OpenAPI
- **WordPress REST API** — Data source (meirtv.com)
- **LiteDB** — Embedded NoSQL database for local caching
- **Polly** — Resilience and transient-fault-handling
- **TagLibSharp** — Audio file metadata tagging
- **WiX v6** — Windows Installer (MSI)
- **System.Text.Json** — JSON serialization
- **IAsyncEnumerable** — Streaming/incremental data loading

## 📄 License

MIT
