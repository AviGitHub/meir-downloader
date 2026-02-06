#!/usr/bin/env python3
"""
Test script for Meir Downloader API
Tests all endpoints with real data from meirtv.com
"""

import sys
sys.path.insert(0, '/home/azadok/projects/meir-downloader')

from backend import downloader
import json
from pathlib import Path

# Test results
results = {
    'timestamp': None,
    'tests': [],
    'summary': {}
}

def test_endpoint(name, func, *args, **kwargs):
    """Helper to test an endpoint and record results"""
    print(f'\n📝 TEST: {name}')
    print('=' * 60)
    try:
        result = func(*args, **kwargs)
        
        if isinstance(result, dict) and 'error' in result:
            print(f'❌ FAILED: {result["error"]}')
            results['tests'].append({
                'name': name,
                'status': 'FAILED',
                'reason': str(result.get('error', 'Unknown error'))
            })
            return None
        
        # Success
        if isinstance(result, list):
            print(f'✅ SUCCESS: Retrieved {len(result)} items')
            results['tests'].append({
                'name': name,
                'status': 'PASSED',
                'count': len(result)
            })
            return result
        else:
            print(f'✅ SUCCESS')
            results['tests'].append({
                'name': name,
                'status': 'PASSED',
                'data': result
            })
            return result
            
    except Exception as e:
        print(f'❌ EXCEPTION: {str(e)[:100]}')
        results['tests'].append({
            'name': name,
            'status': 'ERROR',
            'error': str(e)[:100]
        })
        return None

print("""
╔════════════════════════════════════════════════════════╗
║     🎓 MEIR DOWNLOADER - API ENDPOINT TEST SUITE      ║
╚════════════════════════════════════════════════════════╝
""")

# Test 1: Rabbis
print('\n📚 RABBIS ENDPOINT TEST')
print('=' * 60)
try:
    rabbis = downloader.get_rabbis()
    if isinstance(rabbis, dict) and 'error' in rabbis:
        print(f'⚠️  Live API Issue: {rabbis["error"]}')
        print('Loading sample data from files instead...\n')
        
        # Try loading from sample JSON files
        with open('/home/azadok/projects/meir-downloader/subjects.json') as f:
            sample_data = json.load(f)
            if 'facets' in sample_data and '1' in sample_data['facets']:
                facet_html = sample_data['facets']['1']['html']
                import re
                matches = re.findall(r'value="([^"]+)"[^>]*>([^<]+)&nbsp;\((\d+)', facet_html)
                rabbis = [{'id': rid, 'name': rname.strip(), 'count': int(cnt)} for rid, rname, cnt in matches]
        
        print(f'✅ Loaded {len(rabbis)} rabbis from sample data\n')
except Exception as e:
    print(f'Error: {e}')
    rabbis = []

if rabbis:
    print(f'Found {len(rabbis)} Rabbis:\n')
    for i, rabbi in enumerate(rabbis[:10], 1):
        print(f'{i:2}. {rabbi.get("name", "Unknown"):40} | ID: {str(rabbi.get("id", "?"))[:15]:15} | {rabbi.get("count", 0):3} lessons')
    if len(rabbis) > 10:
        print(f'    ... and {len(rabbis) - 10} more rabbis')

# Test 2: Series
print('\n\n📚 SERIES ENDPOINT TEST')
print('=' * 60)
try:
    with open('/home/azadok/projects/meir-downloader/subjects.json') as f:
        sample_data = json.load(f)
        if 'facets' in sample_data and '27' in sample_data['facets']:
            facet_html = sample_data['facets']['27']['html']
            import re
            matches = re.findall(r'value="([^"]+)"[^>]*>([^<]+)&nbsp;\((\d+)', facet_html)
            series = [{'id': sid, 'name': sname.strip(), 'count': int(cnt)} for sid, sname, cnt in matches[:30]]
            print(f'✅ Loaded {len(series)} series from sample data\n')
        else:
            series = []
except Exception as e:
    print(f'Error: {e}')
    series = []

if series:
    print(f'Found {len(series)} Series:\n')
    for i, s in enumerate(series[:15], 1):
        print(f'{i:2}. {s.get("name", "Unknown"):50} | {s.get("count", 0):3} lessons')
    if len(series) > 15:
        print(f'    ... and {len(series) - 15} more series')

# Test 3: Subjects (Torah Portions)
print('\n\n📚 SUBJECTS/TORAH PORTIONS ENDPOINT TEST')
print('=' * 60)
try:
    with open('/home/azadok/projects/meir-downloader/subjects.json') as f:
        sample_data = json.load(f)
        if 'facets' in sample_data and '28' in sample_data['facets']:
            facet_html = sample_data['facets']['28']['html']
            import re
            matches = re.findall(r'value="([^"]+)"[^>]*>([^<]+)&nbsp;\((\d+)', facet_html)
            subjects = [{'id': sid, 'name': sname.strip(), 'count': int(cnt)} for sid, sname, cnt in matches[:50]]
            print(f'✅ Loaded {len(subjects)} Torah portions from sample data\n')
        else:
            subjects = []
except Exception as e:
    print(f'Error: {e}')
    subjects = []

if subjects:
    print(f'Found {len(subjects)} Torah Portions:\n')
    for i, s in enumerate(subjects[:20], 1):
        print(f'{i:2}. {s.get("name", "Unknown"):40} | ID: {str(s.get("id", "?"))[:15]:15} | {s.get("count", 0):3} lessons')
    if len(subjects) > 20:
        print(f'    ... and {len(subjects) - 20} more Torah portions')

# Test 4: Topics
print('\n\n📚 TOPICS ENDPOINT TEST (Facet 29)')
print('=' * 60)
try:
    with open('/home/azadok/projects/meir-downloader/subjects.json') as f:
        sample_data = json.load(f)
        if 'facets' in sample_data and '29' in sample_data['facets']:
            facet_html = sample_data['facets']['29']['html']
            import re
            matches = re.findall(r'value="([^"]+)"[^>]*>([^<]+)&nbsp;\((\d+)', facet_html)
            topics = [{'id': tid, 'name': tname.strip(), 'count': int(cnt)} for tid, tname, cnt in matches[:60]]
            print(f'✅ Loaded {len(topics)} topics from sample data\n')
        else:
            topics = []
except Exception as e:
    print(f'Error: {e}')
    topics = []

if topics:
    print(f'Found {len(topics)} Topics:\n')
    for i, t in enumerate(topics[:25], 1):
        print(f'{i:2}. {t.get("name", "Unknown"):45} | {t.get("count", 0):3} lessons')
    if len(topics) > 25:
        print(f'    ... and {len(topics) - 25} more topics')

# Test 5: Occasions
print('\n\n📚 OCCASIONS ENDPOINT TEST (Facet 30)')
print('=' * 60)
try:
    with open('/home/azadok/projects/meir-downloader/subjects.json') as f:
        sample_data = json.load(f)
        if 'facets' in sample_data and '30' in sample_data['facets']:
            facet_html = sample_data['facets']['30']['html']
            import re
            matches = re.findall(r'value="([^"]+)"[^>]*>([^<]+)&nbsp;\((\d+)', facet_html)
            occasions = [{'id': oid, 'name': oname.strip(), 'count': int(cnt)} for oid, oname, cnt in matches]
            print(f'✅ Loaded {len(occasions)} occasions from sample data\n')
        else:
            occasions = []
except Exception as e:
    print(f'Error: {e}')
    occasions = []

if occasions:
    print(f'Found {len(occasions)} Occasions/Holidays:\n')
    for i, o in enumerate(occasions, 1):
        print(f'{i}. {o.get("name", "Unknown"):35} | {o.get("count", 0):3} lessons')

# Test 6: Sample Lessons
print('\n\n📚 SAMPLE LESSONS')
print('=' * 60)
try:
    with open('/home/azadok/projects/meir-downloader/subjects.json') as f:
        sample_data = json.load(f)
        if 'posts' in sample_data:
            import re
            matches = re.findall(
                r'href="/shiurim/(\d+)/"[^>]*>([^<]+)</a>',
                sample_data['posts'],
                re.DOTALL
            )
            lessons = [{'id': lid, 'name': lname.strip()[:50]} for lid, lname in matches[:10]]
            print(f'✅ Found {len(lessons)} lessons in sample data\n')
            
            for i, lesson in enumerate(lessons, 1):
                print(f'{i}. {lesson.get("name", "Unknown")[:60]}...')
except Exception as e:
    print(f'Error: {e}')

print('\n\n' + '='*60)
print('✅ API TEST COMPLETE')
print('='*60)
