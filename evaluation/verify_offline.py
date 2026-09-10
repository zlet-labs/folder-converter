import sys
import os
import socket
import time
from pathlib import Path

# Track any outgoing socket connections
original_socket_connect = socket.socket.connect
connections_attempted = []

def monitored_connect(self, address):
    connections_attempted.append(address)
    # Block network
    raise OSError(f"Network blocked by offline verification test (attempted connect to {address})")

socket.socket.connect = monitored_connect

# Set environment variables for offline mode
os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["TRANSFORMERS_OFFLINE"] = "1"
os.environ["HTTP_PROXY"] = "http://0.0.0.0:1"
os.environ["HTTPS_PROXY"] = "http://0.0.0.0:1"

print("--- Starting Offline Mode Verification ---")
print(f"HF_HUB_OFFLINE: {os.environ.get('HF_HUB_OFFLINE')}")
print(f"Socket monitoring: ACTIVE (all outgoing network calls blocked)")

fixtures_to_test = [
    Path("evaluation/fixtures/F01_simple_text.pdf"),
    Path("evaluation/fixtures/F06_scanned.pdf"),
    Path("evaluation/fixtures/F08_structured.docx")
]

try:
    from docling.document_converter import DocumentConverter
    converter = DocumentConverter()
    print("DocumentConverter initialized in offline mode successfully.")
    
    for f in fixtures_to_test:
        t0 = time.perf_counter()
        res = converter.convert(str(f))
        md = res.document.export_to_markdown()
        dur = time.perf_counter() - t0
        print(f"Offline conversion {f.name}: SUCCESS ({dur:.2f}s, {len(md)} chars)")
        
except Exception as ex:
    print(f"Offline conversion FAILED with error: {type(ex).__name__}: {ex}")

print(f"\nTotal network connection attempts intercepted: {len(connections_attempted)}")
if connections_attempted:
    for c in connections_attempted:
        print(f"  Attempted: {c}")
else:
    print("  Zero network connections attempted! Fully local/offline execution verified.")
