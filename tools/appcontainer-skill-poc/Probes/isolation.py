"""Adversarial checks against synthetic, runner-owned fixtures only."""
import json
import os
import socket
import subprocess
import sys

request = json.loads(sys.stdin.read())
result = {}
try:
    with open(request['outside_read'], encoding='utf-8') as file:
        result['outside_read'] = {'allowed': True, 'content': file.read(32)}
except OSError as error:
    result['outside_read'] = {'allowed': False, 'winerror': getattr(error, 'winerror', None), 'errno': error.errno}
try:
    with open(request['outside_write'], 'w', encoding='utf-8') as file:
        file.write('unexpected')
    result['outside_write'] = {'allowed': True}
except OSError as error:
    result['outside_write'] = {'allowed': False, 'winerror': getattr(error, 'winerror', None), 'errno': error.errno}
try:
    with socket.create_connection(('1.1.1.1', 443), timeout=3):
        result['network'] = {'allowed': True}
except OSError as error:
    result['network'] = {'allowed': False, 'winerror': getattr(error, 'winerror', None), 'errno': error.errno}
try:
    outcome = subprocess.run([sys.executable, '-I', '-S', '-B', '-c', 'print("child")'], capture_output=True, timeout=3)
    result['child'] = {'allowed': True, 'exit': outcome.returncode}
except OSError as error:
    result['child'] = {'allowed': False, 'winerror': getattr(error, 'winerror', None)}
except subprocess.TimeoutExpired:
    result['child'] = {'allowed': True, 'timed_out': True}
result['inherited_secret'] = os.environ.get('TABLECLOTH_POC_SYNTHETIC_SECRET')
with open('allowed-write.txt', 'w', encoding='utf-8') as file:
    file.write('allowed')
result['work_write'] = True
print(json.dumps(result))
