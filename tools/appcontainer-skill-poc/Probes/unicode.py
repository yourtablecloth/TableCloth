import json
import sys

value = json.loads(sys.stdin.read())
print(json.dumps({'text': value['text'], 'stdout_encoding': sys.stdout.encoding}, ensure_ascii=False))
