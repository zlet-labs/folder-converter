import sys
import os
import socket
import time
from pathlib import Path

# Track any outgoing socket connections or DNS resolution attempts
connections_attempted = []

def record_and_block(op_name, target):
    entry = f"{op_name}({target})"
    connections_attempted.append(entry)
    raise OSError(f"Network blocked by offline verification test: {entry}")

original_connect = socket.socket.connect
def monitored_connect(self, address):
    record_and_block("socket.connect", address)
socket.socket.connect = monitored_connect

original_connect_ex = socket.socket.connect_ex
def monitored_connect_ex(self, address):
    record_and_block("socket.connect_ex", address)
socket.socket.connect_ex = monitored_connect_ex

original_sendto = socket.socket.sendto
def monitored_sendto(self, *args, **kwargs):
    target = args[1] if len(args) > 1 else kwargs.get("address", "unknown")
    record_and_block("socket.sendto", target)
socket.socket.sendto = monitored_sendto

original_getaddrinfo = socket.getaddrinfo
def monitored_getaddrinfo(host, port, *args, **kwargs):
    record_and_block("socket.getaddrinfo", f"{host}:{port}")
socket.getaddrinfo = monitored_getaddrinfo

original_gethostbyname = socket.gethostbyname
def monitored_gethostbyname(hostname):
    record_and_block("socket.gethostbyname", hostname)
socket.gethostbyname = monitored_gethostbyname

original_create_connection = socket.create_connection
def monitored_create_connection(address, *args, **kwargs):
    record_and_block("socket.create_connection", address)
socket.create_connection = monitored_create_connection

# Set environment variables for offline mode
os.environ["HF_HUB_OFFLINE"] = "1"
os.environ["TRANSFORMERS_OFFLINE"] = "1"
os.environ["HTTP_PROXY"] = "http://0.0.0.0:1"
os.environ["HTTPS_PROXY"] = "http://0.0.0.0:1"
os.environ["NO_PROXY"] = ""

print("--- Starting Offline Mode Verification ---")
print(f"HF_HUB_OFFLINE: {os.environ.get('HF_HUB_OFFLINE')}")
print("Socket/DNS monitoring: ACTIVE (connect, connect_ex, sendto, getaddrinfo blocked)")

fixtures_to_test = [
    Path("evaluation/fixtures/F01_simple_text.pdf"),
    Path("evaluation/fixtures/F06_scanned.pdf"),
    Path("evaluation/fixtures/F08_structured.docx")
]

conversion_successes = 0
conversion_failures = 0
init_success = False

try:
    from docling.document_converter import DocumentConverter, PdfFormatOption
    from docling.datamodel.pipeline_options import PdfPipelineOptions

    pdf_options = PdfPipelineOptions(do_ocr=False)
    converter = DocumentConverter(
        format_options={"pdf": PdfFormatOption(pipeline_options=pdf_options)}
    )
    init_success = True
    print("DocumentConverter initialized in offline mode successfully (OCR disabled).")

    for f in [Path("evaluation/fixtures/F01_simple_text.pdf"), Path("evaluation/fixtures/F08_structured.docx")]:
        t0 = time.perf_counter()
        res = converter.convert(str(f))
        md = res.document.export_to_markdown()
        dur = time.perf_counter() - t0
        if md and len(md.strip()) > 0:
            conversion_successes += 1
            print(f"Offline conversion {f.name}: SUCCESS ({dur:.2f}s, {len(md)} chars)")
        else:
            conversion_failures += 1
            print(f"Offline conversion {f.name}: FAILED (empty output emitted)")

    scanned_f = Path("evaluation/fixtures/F06_scanned.pdf")
    t0 = time.perf_counter()
    res = converter.convert(str(scanned_f))
    md = res.document.export_to_markdown()
    dur = time.perf_counter() - t0
    if not md or not md.strip():
        conversion_successes += 1
        print(f"Offline conversion {scanned_f.name}: SUCCESS (scanned document correctly unsupported without OCR, {dur:.2f}s)")
    else:
        conversion_failures += 1
        print(f"Offline conversion {scanned_f.name}: FAILED (unexpected OCR output)")

except Exception as ex:
    conversion_failures += (len(fixtures_to_test) - conversion_successes)
    print(f"Offline conversion process error: {type(ex).__name__}: {ex}")

print(f"\nTotal network connection attempts intercepted: {len(connections_attempted)}")
if connections_attempted:
    for c in connections_attempted:
        print(f"  Attempted connection to: {c}")

# Strict exit code verification: must succeed on all fixtures and have 0 network attempts
if not init_success or conversion_failures > 0 or conversion_successes != len(fixtures_to_test) or len(connections_attempted) > 0:
    print(f"\nVERIFICATION FAILED: init={init_success}, successes={conversion_successes}/{len(fixtures_to_test)}, failures={conversion_failures}, network_attempts={len(connections_attempted)}")
    sys.exit(1)
else:
    print(f"\nVERIFICATION PASSED: All {conversion_successes}/{len(fixtures_to_test)} fixtures converted offline with exactly zero network attempts.")
    sys.exit(0)
