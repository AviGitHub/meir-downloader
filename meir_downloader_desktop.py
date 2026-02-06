"""
Meir Downloader - Desktop Application
A modern desktop app for downloading lessons from Machon Meir
"""

import sys
import os
import json
import asyncio
import requests
from pathlib import Path
from datetime import datetime
from typing import Optional, List, Dict

from PyQt6.QtWidgets import (
    QApplication, QMainWindow, QWidget, QVBoxLayout, QHBoxLayout,
    QComboBox, QPushButton, QTableWidget, QTableWidgetItem, QLabel,
    QProgressBar, QMessageBox, QSplitter, QHeaderView, QStatusBar,
    QFileDialog, QSpinBox, QCheckBox, QLineEdit
)
from PyQt6.QtCore import Qt, QThread, pyqtSignal, QSize, QTimer
from PyQt6.QtGui import QIcon, QColor, QFont, QPixmap, QStandardItemModel, QStandardItem
from PyQt6.QtCharts import QChart, QChartView, QBarSeries, QBarSet, QBarCategoryAxis, QValueAxis
from PyQt6.QtCore import QRectF


class DownloadWorker(QThread):
    """Worker thread for downloading files"""
    progress_update = pyqtSignal(int)
    download_complete = pyqtSignal(bool, str)
    
    def __init__(self, url: str, filepath: str, lesson_data: dict):
        super().__init__()
        self.url = url
        self.filepath = filepath
        self.lesson_data = lesson_data
        
    def run(self):
        try:
            response = requests.get(self.url, stream=True)
            total_size = int(response.headers.get('content-length', 0))
            
            Path(self.filepath).parent.mkdir(parents=True, exist_ok=True)
            
            downloaded = 0
            with open(self.filepath, 'wb') as f:
                for chunk in response.iter_content(chunk_size=8192):
                    if chunk:
                        f.write(chunk)
                        downloaded += len(chunk)
                        if total_size > 0:
                            progress = int((downloaded / total_size) * 100)
                            self.progress_update.emit(progress)
            
            self.download_complete.emit(True, "הורדה בוצעה בהצלחה")
        except Exception as e:
            self.download_complete.emit(False, f"שגיאה: {str(e)}")


class MeirDownloaderApp(QMainWindow):
    def __init__(self):
        super().__init__()
        
        # API endpoint
        self.API_URL = "https://meirtv.com/wp-admin/admin-ajax.php"
        self.DOWNLOAD_PATH = Path.home() / "מוריד שיעורים"
        self.DOWNLOAD_PATH.mkdir(exist_ok=True)
        
        # Data
        self.rabbis = []
        self.topics = []
        self.lessons = []
        self.downloads = {}  # Track downloads
        self.downloading_threads = {}
        
        self.init_ui()
        self.load_data()
        
    def init_ui(self):
        """Initialize the user interface"""
        self.setWindowTitle("🎓 מוריד שיעורים - Meir Downloader")
        self.setWindowIcon(self.create_icon())
        self.setGeometry(100, 100, 1400, 900)
        
        # Main widget
        main_widget = QWidget()
        self.setCentralWidget(main_widget)
        layout = QHBoxLayout(main_widget)
        
        # Left panel - Filters
        left_panel = self.create_filters_panel()
        
        # Right panel - Lessons
        right_panel = self.create_lessons_panel()
        
        # Splitter
        splitter = QSplitter(Qt.Orientation.Horizontal)
        splitter.addWidget(left_panel)
        splitter.addWidget(right_panel)
        splitter.setStretchFactor(0, 1)
        splitter.setStretchFactor(1, 2)
        
        layout.addWidget(splitter)
        
        # Status bar
        self.status_bar = QStatusBar()
        self.setStatusBar(self.status_bar)
        self.status_bar.showMessage("נטען...")
        
    def create_icon(self) -> QIcon:
        """Create application icon"""
        pixmap = QPixmap(64, 64)
        pixmap.fill(QColor(102, 126, 234))
        return QIcon(pixmap)
    
    def create_filters_panel(self) -> QWidget:
        """Create filters panel"""
        panel = QWidget()
        layout = QVBoxLayout(panel)
        
        # Title
        title = QLabel("📋 סינון")
        title_font = QFont()
        title_font.setPointSize(14)
        title_font.setBold(True)
        title.setFont(title_font)
        layout.addWidget(title)
        
        # Rabbi selector
        layout.addWidget(QLabel("בחר רב:"))
        self.rabbi_combo = QComboBox()
        self.rabbi_combo.currentIndexChanged.connect(self.on_rabbi_changed)
        layout.addWidget(self.rabbi_combo)
        
        # Topic selector
        layout.addWidget(QLabel("בחר נושא:"))
        self.topic_combo = QComboBox()
        self.topic_combo.currentIndexChanged.connect(self.on_topic_changed)
        layout.addWidget(self.topic_combo)
        
        # Search
        layout.addWidget(QLabel("חפש:"))
        self.search_box = QLineEdit()
        self.search_box.setPlaceholderText("חפש שם שיעור...")
        self.search_box.textChanged.connect(self.filter_lessons)
        layout.addWidget(self.search_box)
        
        # Buttons
        self.reset_btn = QPushButton("🔄 אפס סינון")
        self.reset_btn.clicked.connect(self.reset_filters)
        layout.addWidget(self.reset_btn)
        
        self.open_folder_btn = QPushButton("📂 פתח תיקייה")
        self.open_folder_btn.clicked.connect(self.open_downloads_folder)
        layout.addWidget(self.open_folder_btn)
        
        layout.addStretch()
        
        return panel
    
    def create_lessons_panel(self) -> QWidget:
        """Create lessons panel"""
        panel = QWidget()
        layout = QVBoxLayout(panel)
        
        # Title
        title = QLabel("📚 שיעורים")
        title_font = QFont()
        title_font.setPointSize(14)
        title_font.setBold(True)
        title.setFont(title_font)
        layout.addWidget(title)
        
        # Lessons table
        self.table = QTableWidget()
        self.table.setColumnCount(6)
        self.table.setHorizontalHeaderLabels(["שם", "רב", "סדרה", "שיעור", "תאריך", "פעולה"])
        self.table.horizontalHeader().setStretchLastSection(False)
        self.table.setColumnWidth(0, 200)
        self.table.setColumnWidth(1, 150)
        self.table.setColumnWidth(2, 150)
        self.table.setColumnWidth(3, 50)
        self.table.setColumnWidth(4, 100)
        self.table.setColumnWidth(5, 100)
        layout.addWidget(self.table)
        
        # Downloads panel
        downloads_title = QLabel("⬇️ הורדות פעילות")
        downloads_font = QFont()
        downloads_font.setPointSize(12)
        downloads_font.setBold(True)
        downloads_title.setFont(downloads_font)
        layout.addWidget(downloads_title)
        
        self.downloads_table = QTableWidget()
        self.downloads_table.setColumnCount(4)
        self.downloads_table.setHorizontalHeaderLabels(["שם", "התקדמות", "סטטוס", "פעולה"])
        self.downloads_table.horizontalHeader().setStretchLastSection(True)
        self.downloads_table.setMaximumHeight(150)
        layout.addWidget(self.downloads_table)
        
        return panel
    
    def load_data(self):
        """Load rabbis from API"""
        try:
            params = {
                'action': 'wpgb_get_posts',
                'grid': '1',
                'paged': '1'
            }
            response = requests.post(self.API_URL, data=params, timeout=5)
            data = response.json()
            
            # Parse rabbis from facet 1
            if 'facets' in data and '1' in data['facets']:
                html = data['facets']['1']['html']
                import re
                matches = re.findall(r'data-facet-value="([^"]+)"[^>]*>([^<]+)<[^>]*>(\d+)<', html)
                self.rabbis = [{'id': m[0], 'name': m[1].strip(), 'count': int(m[2])} for m in matches]
                
                self.rabbi_combo.blockSignals(True)
                self.rabbi_combo.clear()
                self.rabbi_combo.addItem("-- בחר רב --", None)
                for rabbi in self.rabbis:
                    self.rabbi_combo.addItem(f"{rabbi['name']} ({rabbi['count']})", rabbi['id'])
                self.rabbi_combo.blockSignals(False)
            
            self.status_bar.showMessage("✅ נתונים נטענו בהצלחה")
        except Exception as e:
            self.status_bar.showMessage(f"❌ שגיאה בטעינת נתונים: {str(e)}")
    
    def on_rabbi_changed(self):
        """Handle rabbi selection"""
        rabbi_id = self.rabbi_combo.currentData()
        if not rabbi_id:
            self.topic_combo.clear()
            self.lessons = []
            self.refresh_lessons_table()
            return
        
        try:
            # Load topics for selected rabbi
            params = {
                'action': 'wpgb_get_posts',
                'grid': '1',
                'paged': '1',
                'facets[1][]': rabbi_id
            }
            response = requests.post(self.API_URL, data=params, timeout=5)
            data = response.json()
            
            # Parse topics from facet 29
            self.topics = []
            if 'facets' in data and '29' in data['facets']:
                html = data['facets']['29']['html']
                import re
                matches = re.findall(r'data-facet-value="([^"]+)"[^>]*>([^<]+)<[^>]*>(\d+)<', html)
                self.topics = [{'id': m[0], 'name': m[1].strip(), 'count': int(m[2])} for m in matches]
            
            # Populate topics combo
            self.topic_combo.blockSignals(True)
            self.topic_combo.clear()
            self.topic_combo.addItem("-- כל הנושאים --", None)
            for topic in self.topics:
                self.topic_combo.addItem(f"{topic['name']} ({topic['count']})", topic['id'])
            self.topic_combo.blockSignals(False)
            
            # Load lessons
            self.load_lessons(rabbi_id, None)
        except Exception as e:
            self.status_bar.showMessage(f"❌ שגיאה: {str(e)}")
    
    def on_topic_changed(self):
        """Handle topic selection"""
        rabbi_id = self.rabbi_combo.currentData()
        topic_id = self.topic_combo.currentData()
        
        if rabbi_id:
            self.load_lessons(rabbi_id, topic_id)
    
    def load_lessons(self, rabbi_id: str, topic_id: Optional[str]):
        """Load lessons from API"""
        try:
            params = {
                'action': 'wpgb_get_posts',
                'grid': '1',
                'paged': '1',
                'facets[1][]': rabbi_id
            }
            if topic_id:
                params['facets[29][]'] = topic_id
            
            response = requests.post(self.API_URL, data=params, timeout=5)
            data = response.json()
            
            self.lessons = []
            if 'posts' in data:
                import re
                for post in data['posts']:
                    html = post.get('html', '')
                    # Extract lesson info from HTML
                    name = re.search(r'<h3[^>]*>([^<]+)<', html)
                    rabbi = re.search(r'<div[^>]*class="[^"]*rabbi[^"]*"[^>]*>([^<]+)<', html)
                    series = re.search(r'<div[^>]*class="[^"]*series[^"]*"[^>]*>([^<]+)<', html)
                    chapter = re.search(r'<span[^>]*>(\d+)<', html)
                    
                    self.lessons.append({
                        'id': post.get('id', ''),
                        'name': name.group(1) if name else 'Unknown',
                        'rabbi': rabbi.group(1) if rabbi else 'Unknown',
                        'series': series.group(1) if series else 'Unknown',
                        'chapter': chapter.group(1) if chapter else '0',
                        'date': datetime.now().strftime('%Y-%m-%d'),
                        'url': post.get('url', '')
                    })
            
            self.refresh_lessons_table()
            self.status_bar.showMessage(f"✅ {len(self.lessons)} שיעורים נטענו")
        except Exception as e:
            self.status_bar.showMessage(f"❌ שגיאה בטעינת שיעורים: {str(e)}")
    
    def filter_lessons(self):
        """Filter lessons by search text"""
        search_text = self.search_box.text().lower()
        for row in range(self.table.rowCount()):
            item = self.table.item(row, 0)
            if item:
                match = search_text in item.text().lower()
                self.table.setRowHidden(row, not match)
    
    def refresh_lessons_table(self):
        """Refresh lessons table"""
        self.table.setRowCount(0)
        
        for lesson in self.lessons:
            row = self.table.rowCount()
            self.table.insertRow(row)
            
            # Name
            name_item = QTableWidgetItem(lesson['name'])
            self.table.setItem(row, 0, name_item)
            
            # Rabbi
            rabbi_item = QTableWidgetItem(lesson['rabbi'])
            self.table.setItem(row, 1, rabbi_item)
            
            # Series
            series_item = QTableWidgetItem(lesson['series'])
            self.table.setItem(row, 2, series_item)
            
            # Chapter
            chapter_item = QTableWidgetItem(str(lesson['chapter']))
            self.table.setItem(row, 3, chapter_item)
            
            # Date
            date_item = QTableWidgetItem(lesson['date'])
            self.table.setItem(row, 4, date_item)
            
            # Download button
            download_btn = QPushButton("⬇️ הורדה")
            download_btn.clicked.connect(lambda checked, l=lesson: self.download_lesson(l))
            self.table.setCellWidget(row, 5, download_btn)
    
    def download_lesson(self, lesson: dict):
        """Download a lesson"""
        if not lesson.get('url'):
            QMessageBox.warning(self, "שגיאה", "אין URL להורדה")
            return
        
        # Create file path
        rabbi_dir = self.DOWNLOAD_PATH / lesson['rabbi']
        series_dir = rabbi_dir / lesson['series']
        filename = f"{lesson['chapter']}-{lesson['name']}.mp3"
        filepath = series_dir / filename
        
        # Add to downloads
        download_id = f"{lesson['id']}_{datetime.now().timestamp()}"
        self.downloads[download_id] = {
            'lesson': lesson,
            'filepath': str(filepath),
            'status': 'הורדה...',
            'progress': 0
        }
        
        # Start download worker
        worker = DownloadWorker(lesson['url'], str(filepath), lesson)
        worker.progress_update.connect(lambda p, did=download_id: self.update_download_progress(did, p))
        worker.download_complete.connect(lambda s, m, did=download_id: self.download_completed(did, s, m))
        
        self.downloading_threads[download_id] = worker
        worker.start()
        
        self.refresh_downloads_table()
    
    def update_download_progress(self, download_id: str, progress: int):
        """Update download progress"""
        if download_id in self.downloads:
            self.downloads[download_id]['progress'] = progress
            self.refresh_downloads_table()
    
    def download_completed(self, download_id: str, success: bool, message: str):
        """Handle download completion"""
        if download_id in self.downloads:
            self.downloads[download_id]['status'] = message
            if success:
                self.downloads[download_id]['progress'] = 100
        
        self.refresh_downloads_table()
    
    def refresh_downloads_table(self):
        """Refresh active downloads table"""
        self.downloads_table.setRowCount(0)
        
        for download_id, download_info in self.downloads.items():
            if download_info['status'] == 'הורדה מושלמת' or download_info['progress'] == 100:
                continue
            
            row = self.downloads_table.rowCount()
            self.downloads_table.insertRow(row)
            
            # Name
            name_item = QTableWidgetItem(download_info['lesson']['name'][:40])
            self.downloads_table.setItem(row, 0, name_item)
            
            # Progress bar
            progress_bar = QProgressBar()
            progress_bar.setValue(download_info['progress'])
            self.downloads_table.setCellWidget(row, 1, progress_bar)
            
            # Status
            status_item = QTableWidgetItem(download_info['status'])
            self.downloads_table.setItem(row, 2, status_item)
            
            # Cancel button
            cancel_btn = QPushButton("❌ ביטול")
            cancel_btn.clicked.connect(lambda checked, did=download_id: self.cancel_download(did))
            self.downloads_table.setCellWidget(row, 3, cancel_btn)
    
    def cancel_download(self, download_id: str):
        """Cancel a download"""
        if download_id in self.downloading_threads:
            self.downloading_threads[download_id].quit()
            self.downloading_threads[download_id].wait()
        
        if download_id in self.downloads:
            del self.downloads[download_id]
        
        self.refresh_downloads_table()
    
    def reset_filters(self):
        """Reset all filters"""
        self.rabbi_combo.setCurrentIndex(0)
        self.topic_combo.setCurrentIndex(0)
        self.search_box.clear()
        self.lessons = []
        self.refresh_lessons_table()
    
    def open_downloads_folder(self):
        """Open downloads folder"""
        import subprocess
        subprocess.Popen(f'explorer "{self.DOWNLOAD_PATH}"')


def main():
    app = QApplication(sys.argv)
    app.setStyle('Fusion')
    
    window = MeirDownloaderApp()
    window.show()
    
    sys.exit(app.exec())


if __name__ == '__main__':
    main()
