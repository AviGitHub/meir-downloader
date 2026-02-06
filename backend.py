#!/usr/bin/env python3
"""
Meir Downloader Backend Server
Handles lesson downloads from meirtv.com
"""

from flask import Flask, jsonify, request, send_from_directory
from flask_cors import CORS
import requests
import json
import os
from pathlib import Path
from urllib.parse import unquote
import re

app = Flask(__name__, static_folder='.', static_url_path='')
CORS(app)

# Constants
BASE_URL = "https://meirtv.com"
API_ENDPOINT = f"{BASE_URL}/wp-admin/admin-ajax.php"
DEFAULT_DOWNLOAD_PATH = Path.home() / "meir-downloader"

# Create download directory if it doesn't exist
DEFAULT_DOWNLOAD_PATH.mkdir(parents=True, exist_ok=True)

class MeirDownloader:
    def __init__(self):
        self.session = requests.Session()
        self.session.headers.update({
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
        })
    
    def get_rabbis(self):
        """Get list of available rabbis"""
        try:
            params = {
                'action': 'wpgb_get_posts',
                'grid': '1',
                'paged': '1'
            }
            response = self.session.post(API_ENDPOINT, data=params)
            response.raise_for_status()
            data = response.json()
            
            # Parse facets to extract rabbis
            rabbis = []
            if 'facets' in data and '1' in data['facets']:
                facet_html = data['facets']['1']['html']
                # Extract rabbi options
                options = re.findall(r'value="([^"]+)"[^>]*>([^<]+)\((\d+)\)', facet_html)
                for value, name, count in options:
                    if value and value != "":
                        rabbis.append({
                            'id': value,
                            'name': name.strip(),
                            'count': int(count)
                        })
            return rabbis
        except Exception as e:
            return {'error': str(e)}
    
    def get_series(self, rabbi_id=None):
        """Get series for a rabbi"""
        try:
            params = {
                'action': 'wpgb_get_posts',
                'grid': '1',
                'paged': '1'
            }
            if rabbi_id:
                params['facets[rabbis]'] = rabbi_id
            
            response = self.session.post(API_ENDPOINT, data=params)
            response.raise_for_status()
            data = response.json()
            
            # Extract series from facets first
            series_list = []
            if 'facets' in data and '27' in data['facets']:
                facet_html = data['facets']['27']['html']
                # Extract series options from facet
                matches = re.findall(r'value="([^"]+)"[^>]*>([^<]+)\s*\(&nbsp;?(\d+)', facet_html)
                for series_id, series_name, count in matches:
                    if series_id and series_id != "":
                        series_list.append({
                            'id': series_id,
                            'name': unquote(series_name.strip()),
                            'count': int(count)
                        })
            
            # Fallback to parsing from posts if facets not available
            if not series_list and 'posts' in data:
                matches = re.findall(r'href="https://meirtv\.com/shiurim-series/([^/"]+)/"[^>]*>([^<]+)</a>', data['posts'])
                seen = set()
                for series_id, series_name in matches:
                    if series_id not in seen:
                        seen.add(series_id)
                        series_list.append({
                            'id': series_id,
                            'name': unquote(series_name.strip()),
                            'count': 0
                        })
            return series_list
        except Exception as e:
            return {'error': str(e)}
    
    def get_lessons(self, rabbi_id=None, series_id=None, subject_id=None, topic_id=None, occasion_id=None, page=1):
        """Get lessons with optional filters"""
        try:
            params = {
                'action': 'wpgb_get_posts',
                'grid': '1',
                'paged': page
            }
            
            if rabbi_id:
                params['facets[rabbis]'] = rabbi_id
            if series_id:
                params['facets[shiurim-series]'] = series_id
            if subject_id:
                params['facets[rabbis_3]'] = subject_id
            if topic_id:
                params['facets[rabbis_3_2]'] = topic_id
            if occasion_id:
                params['facets[rabbis_3_2_2]'] = occasion_id
            
            response = self.session.post(API_ENDPOINT, data=params)
            response.raise_for_status()
            data = response.json()
            
            lessons = []
            if 'posts' in data:
                # Parse lesson cards from HTML
                matches = re.findall(
                    r'wpgb-post-(\d+)[^>]*>.*?'
                    r'href="/rabbis/(\d+)/"[^>]*>([^<]+)</a>.*?'
                    r'href="/shiurim-series/([^/"]+)/"[^>]*>([^<]+)</a>.*?'
                    r'פרק: (\d+).*?'
                    r'(\d+/\d+/\d+).*?'
                    r'(\d+) דקות.*?'
                    r'href="/shiurim/(\d+)/"[^>]*>([^<]+)</a>',
                    data['posts'],
                    re.DOTALL
                )
                
                for lesson_id, rabbi_id, rabbi_name, series_id, series_name, chapter, date, duration, post_id, lesson_name in matches:
                    lessons.append({
                        'id': lesson_id,
                        'post_id': post_id,
                        'rabbi_id': rabbi_id,
                        'rabbi_name': rabbi_name.strip(),
                        'series_id': unquote(series_id),
                        'series_name': unquote(series_name.strip()),
                        'chapter': int(chapter),
                        'date': date,
                        'duration': int(duration),
                        'name': lesson_name.strip()
                    })
            
            return lessons
        except Exception as e:
            return {'error': str(e)}

downloader = MeirDownloader()

# Serve the React app
@app.route('/')
def index():
    """Serve the main React application"""
    return send_from_directory('.', 'index.html')

@app.route('/api/rabbis', methods=['GET'])
def get_rabbis():
    """Get available rabbis"""
    rabbis = downloader.get_rabbis()
    return jsonify({'rabbis': rabbis})

@app.route('/api/series', methods=['GET'])
def get_series():
    """Get series, optionally filtered by rabbi"""
    rabbi_id = request.args.get('rabbi_id')
    series = downloader.get_series(rabbi_id)
    return jsonify({'series': series})

@app.route('/api/lessons', methods=['GET'])
def get_lessons():
    """Get lessons with optional filters"""
    rabbi_id = request.args.get('rabbi_id')
    series_id = request.args.get('series_id')
    subject_id = request.args.get('subject_id')
    topic_id = request.args.get('topic_id')
    occasion_id = request.args.get('occasion_id')
    page = request.args.get('page', 1, type=int)
    
    lessons = downloader.get_lessons(rabbi_id, series_id, subject_id, topic_id, occasion_id, page)
    return jsonify({'lessons': lessons, 'page': page})

@app.route('/api/download', methods=['POST'])
def download_lesson():
    """Download a lesson"""
    data = request.json
    lesson_id = data.get('lesson_id')
    lesson_name = data.get('lesson_name', 'lesson')
    rabbi_name = data.get('rabbi_name', 'unknown')
    series_name = data.get('series_name', 'unknown')
    chapter = data.get('chapter', '00')
    
    # Create directory structure
    lesson_dir = DEFAULT_DOWNLOAD_PATH / rabbi_name / series_name
    lesson_dir.mkdir(parents=True, exist_ok=True)
    
    # Format filename
    filename = f"{str(chapter).zfill(3)}-{lesson_name}.mp3"
    filepath = lesson_dir / filename
    
    # Try to download from meirtv
    try:
        # Get lesson page
        lesson_url = f"{BASE_URL}/shiurim/{lesson_id}/"
        response = requests.get(lesson_url)
        response.raise_for_status()
        
        # Extract audio URL
        audio_match = re.search(r'<audio[^>]*>.*?<source[^>]*src="([^"]+)"', response.text, re.DOTALL)
        if audio_match:
            audio_url = audio_match.group(1)
            if not audio_url.startswith('http'):
                audio_url = BASE_URL + audio_url
            
            # Download audio
            audio_response = requests.get(audio_url, stream=True)
            audio_response.raise_for_status()
            
            with open(filepath, 'wb') as f:
                for chunk in audio_response.iter_content(chunk_size=8192):
                    f.write(chunk)
            
            return jsonify({
                'success': True,
                'filepath': str(filepath),
                'filename': filename
            })
        else:
            return jsonify({'success': False, 'error': 'Audio not found on lesson page'}), 400
    
    except Exception as e:
        return jsonify({'success': False, 'error': str(e)}), 500

@app.route('/api/subjects', methods=['GET'])
def get_subjects():
    """Get available subjects/Torah portions (facet 28)"""
    rabbi_id = request.args.get('rabbi_id')
    series_id = request.args.get('series_id')
    
    try:
        params = {
            'action': 'wpgb_get_posts',
            'grid': '1',
            'paged': '1'
        }
        if rabbi_id:
            params['facets[rabbis]'] = rabbi_id
        if series_id:
            params['facets[shiurim-series]'] = series_id
        
        response = downloader.session.post(API_ENDPOINT, data=params)
        response.raise_for_status()
        data = response.json()
        
        subjects = []
        if 'facets' in data and '28' in data['facets']:
            facet_html = data['facets']['28']['html']
            matches = re.findall(r'value="([^"]+)"[^>]*>([^<]+)\s*\(&nbsp;?(\d+)', facet_html)
            for subject_id, subject_name, count in matches:
                if subject_id and subject_id != "":
                    subjects.append({
                        'id': subject_id,
                        'name': subject_name.strip(),
                        'count': int(count)
                    })
        
        return jsonify({'subjects': subjects})
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/api/topics', methods=['GET'])
def get_topics():
    """Get available topics/subjects (facet 29)"""
    rabbi_id = request.args.get('rabbi_id')
    series_id = request.args.get('series_id')
    subject_id = request.args.get('subject_id')
    
    try:
        params = {
            'action': 'wpgb_get_posts',
            'grid': '1',
            'paged': '1'
        }
        if rabbi_id:
            params['facets[rabbis]'] = rabbi_id
        if series_id:
            params['facets[shiurim-series]'] = series_id
        if subject_id:
            params['facets[rabbis_3]'] = subject_id
        
        response = downloader.session.post(API_ENDPOINT, data=params)
        response.raise_for_status()
        data = response.json()
        
        topics = []
        if 'facets' in data and '29' in data['facets']:
            facet_html = data['facets']['29']['html']
            matches = re.findall(r'value="([^"]+)"[^>]*>([^<]+)\s*\(&nbsp;?(\d+)', facet_html)
            for topic_id, topic_name, count in matches:
                if topic_id and topic_id != "":
                    topics.append({
                        'id': topic_id,
                        'name': topic_name.strip(),
                        'count': int(count)
                    })
        
        return jsonify({'topics': topics})
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/api/occasions', methods=['GET'])
def get_occasions():
    """Get available occasions/holidays (facet 30)"""
    rabbi_id = request.args.get('rabbi_id')
    series_id = request.args.get('series_id')
    subject_id = request.args.get('subject_id')
    topic_id = request.args.get('topic_id')
    
    try:
        params = {
            'action': 'wpgb_get_posts',
            'grid': '1',
            'paged': '1'
        }
        if rabbi_id:
            params['facets[rabbis]'] = rabbi_id
        if series_id:
            params['facets[shiurim-series]'] = series_id
        if subject_id:
            params['facets[rabbis_3]'] = subject_id
        if topic_id:
            params['facets[rabbis_3_2]'] = topic_id
        
        response = downloader.session.post(API_ENDPOINT, data=params)
        response.raise_for_status()
        data = response.json()
        
        occasions = []
        if 'facets' in data and '30' in data['facets']:
            facet_html = data['facets']['30']['html']
            matches = re.findall(r'value="([^"]+)"[^>]*>([^<]+)\s*\(&nbsp;?(\d+)', facet_html)
            for occasion_id, occasion_name, count in matches:
                if occasion_id and occasion_id != "":
                    occasions.append({
                        'id': occasion_id,
                        'name': occasion_name.strip(),
                        'count': int(count)
                    })
        
        return jsonify({'occasions': occasions})
    except Exception as e:
        return jsonify({'error': str(e)}), 500

@app.route('/api/config', methods=['GET'])
def get_config():
    """Get app configuration"""
    return jsonify({
        'download_path': str(DEFAULT_DOWNLOAD_PATH),
        'base_url': BASE_URL
    })

@app.route('/health', methods=['GET'])
def health():
    """Health check endpoint"""
    return jsonify({'status': 'ok'})

if __name__ == '__main__':
    print(f"🎓 Meir Downloader Backend")
    print(f"📁 Downloads to: {DEFAULT_DOWNLOAD_PATH}")
    print(f"🌐 Starting server on http://localhost:5000")
    app.run(debug=True, port=5000)
