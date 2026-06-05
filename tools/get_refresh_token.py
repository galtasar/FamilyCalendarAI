"""
Run this script to generate a new Gmail/Google Calendar refresh token.

Set environment variables first (or it will prompt):
  GMAIL_CLIENT_ID=...
  GMAIL_CLIENT_SECRET=...

Usage: python get_refresh_token.py
"""
import os
import urllib.parse
import urllib.request
import json
import webbrowser
from http.server import HTTPServer, BaseHTTPRequestHandler
import threading

CLIENT_ID = os.environ.get("GMAIL_CLIENT_ID") or input("Enter GMAIL_CLIENT_ID: ").strip()
CLIENT_SECRET = os.environ.get("GMAIL_CLIENT_SECRET") or input("Enter GMAIL_CLIENT_SECRET: ").strip()
REDIRECT_URI = "http://localhost:8080"
SCOPES = [
    "https://www.googleapis.com/auth/gmail.modify",
    "https://www.googleapis.com/auth/calendar",
]

auth_code = None

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        global auth_code
        params = urllib.parse.parse_qs(urllib.parse.urlparse(self.path).query)
        auth_code = params.get("code", [None])[0]
        self.send_response(200)
        self.send_header("Content-type", "text/html")
        self.end_headers()
        self.wfile.write(b"<h2>Done! You can close this tab and return to the terminal.</h2>")
    def log_message(self, *args):
        pass  # silence request logs

server = HTTPServer(("localhost", 8080), Handler)
thread = threading.Thread(target=server.handle_request)
thread.start()

auth_url = (
    "https://accounts.google.com/o/oauth2/auth?"
    + urllib.parse.urlencode({
        "client_id": CLIENT_ID,
        "redirect_uri": REDIRECT_URI,
        "response_type": "code",
        "scope": " ".join(SCOPES),
        "access_type": "offline",
        "prompt": "consent",
    })
)

print("\nOpening browser for authorization...")
print("Sign in as dahl.aicalendar@gmail.com and allow access.\n")
webbrowser.open(auth_url)

thread.join(timeout=120)
server.server_close()

if not auth_code:
    print("❌ No code received. Try again.")
    exit(1)

print("✅ Got authorization code, exchanging for tokens...")

data = urllib.parse.urlencode({
    "code": auth_code,
    "client_id": CLIENT_ID,
    "client_secret": CLIENT_SECRET,
    "redirect_uri": REDIRECT_URI,
    "grant_type": "authorization_code",
}).encode()

req = urllib.request.Request(
    "https://oauth2.googleapis.com/token",
    data=data,
    headers={"Content-Type": "application/x-www-form-urlencoded"},
    method="POST",
)

with urllib.request.urlopen(req) as resp:
    tokens = json.loads(resp.read())

refresh_token = tokens.get("refresh_token")
if refresh_token:
    print("\n✅ New refresh token:")
    print(refresh_token)
    print("\nUpdate GMAIL_REFRESH_TOKEN in the .env on Oracle Cloud with this value.")
else:
    print("\n❌ No refresh token returned:", tokens)
