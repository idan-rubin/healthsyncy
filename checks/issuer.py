"""Loopback-only issuer for the disposable HTTP integration test."""
import base64
import json
import ssl
import subprocess
import sys
import time
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

work = Path(sys.argv[1])
key = work / "issuer.key"
issuer = "https://localhost:5510"
resource = "https://synthetic.invalid/mcp"

def b64(data):
    return base64.urlsafe_b64encode(data).decode().rstrip("=")

def encode(value):
    return b64(json.dumps(value, separators=(",", ":")).encode())

modulus = subprocess.check_output(["openssl", "rsa", "-in", key, "-noout", "-modulus"], text=True)
jwk = {"kty": "RSA", "alg": "RS256", "use": "sig", "kid": "fixture",
       "e": "AQAB", "n": b64(bytes.fromhex(modulus.strip().split("=")[1]))}
now = int(time.time())
claims = {"iss": issuer, "sub": "synthetic-owner", "aud": resource, "scope": "activity.read",
          "iat": now, "exp": now + 1800}
for name, override in {"valid": {}, "wrong-user": {"sub": "other"},
                       "wrong-scope": {"scope": "unrelated"}, "wrong-aud": {"aud": "other"},
                       "expired": {"exp": now - 300}}.items():
    header = {"alg": "RS256", "kid": "fixture", "typ": "JWT"}
    message = encode(header) + "." + encode(claims | override)
    sig = subprocess.check_output(["openssl", "dgst", "-sha256", "-sign", key], input=message.encode())
    (work / (name + ".jwt")).write_text(message + "." + b64(sig))

discovery = {"issuer": issuer, "jwks_uri": issuer + "/keys", "authorization_endpoint": issuer + "/authorize",
             "token_endpoint": issuer + "/token", "response_types_supported": ["code"],
             "subject_types_supported": ["public"], "id_token_signing_alg_values_supported": ["RS256"]}

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        result = {"/keys": {"keys": [jwk]}, "/.well-known/openid-configuration": discovery}.get(self.path)
        if result is None:
            self.send_error(404)
            return
        body = json.dumps(result).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

server = HTTPServer(("127.0.0.1", 5510), Handler)
ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
ctx.load_cert_chain(work / "issuer.crt", key)
server.socket = ctx.wrap_socket(server.socket, server_side=True)
server.serve_forever()
