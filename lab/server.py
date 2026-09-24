"""Small real dependency failure for the local incident lab; no simulated signals."""

import json
import os
import time
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

ROLE = os.environ.get("SERVICE_ROLE", "checkout")
INVENTORY_URL = os.environ.get("INVENTORY_URL", "http://inventory/inventory")


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        started = time.monotonic()
        status = 200
        if self.path in ("/healthz", "/readyz"):
            result = {"status": "ready", "service": ROLE}
        elif ROLE == "inventory" and self.path == "/inventory":
            result = {"item": "demo-item", "available": True}
        elif ROLE == "checkout" and self.path == "/checkout":
            try:
                with urllib.request.urlopen(INVENTORY_URL, timeout=2) as response:
                    inventory = json.load(response)
                result = {"status": "ok", "inventory": inventory}
            except (urllib.error.URLError, TimeoutError, OSError, ValueError) as error:
                status = 503
                result = {
                    "status": "unavailable",
                    "dependency": "inventory",
                    "error": str(error),
                }
        else:
            status = 404
            result = {"error": "unknown endpoint"}
        body = json.dumps(result).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)
        if self.path not in ("/healthz", "/readyz"):
            print(
                json.dumps({
                    "at": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                    "service": ROLE,
                    "path": self.path,
                    "status": status,
                    "duration_ms": round((time.monotonic() - started) * 1000, 2),
                    **(
                        {"dependency_url": INVENTORY_URL, "error": result["error"]}
                        if status == 503
                        else {}
                    ),
                }),
                flush=True,
            )

    def log_message(self, format, *args):
        pass


if __name__ == "__main__":
    ThreadingHTTPServer(("0.0.0.0", 8080), Handler).serve_forever()
