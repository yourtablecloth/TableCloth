"""Allocate beyond the configured 64 MiB job limit using synthetic data."""
import json

try:
    allocation = bytearray(256 * 1024 * 1024)
    print(json.dumps({'allocation_succeeded': True}))
except MemoryError:
    print(json.dumps({'allocation_succeeded': False, 'memory_error': True}))
