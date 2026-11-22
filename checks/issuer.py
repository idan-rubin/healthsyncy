"""Loopback-only issuer for the disposable HTTP integration test."""
import base64
import hashlib
import secrets
from urllib.parse import parse_qs, urlencode, urlsplit
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
def signed(payload):
    message = encode({"alg": "RS256", "kid": "fixture", "typ": "JWT"}) + "." + encode(payload)
    sig = subprocess.check_output(["openssl", "dgst", "-sha256", "-sign", key], input=message.encode())
    return message + "." + b64(sig)

codes = {}
for name, override in {"valid": {}, "wrong-user": {"sub": "other"},
                       "wrong-scope": {"scope": "unrelated"}, "wrong-aud": {"aud": "other"},
                       "expired": {"exp": now - 300}}.items():
    (work / (name + ".jwt")).write_text(signed(claims | override))

discovery = {"issuer": issuer, "jwks_uri": issuer + "/keys", "authorization_endpoint": issuer + "/authorize",
             "token_endpoint": issuer + "/token", "response_types_supported": ["code"],
             "subject_types_supported": ["public"], "id_token_signing_alg_values_supported": ["RS256"]}

class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        url = urlsplit(self.path)
        if url.path == "/authorize":
            q = parse_qs(url.query)
            if (q["client_id"] != ["synthetic-client"] or q["code_challenge_method"] != ["S256"]
                    or q["redirect_uri"] != ["https://localhost:5513/signin-oidc"]):
                self.send_error(400)
                return
            code = secrets.token_urlsafe(32)
            codes[code] = q
            self.send_response(302)
            self.send_header("Location", q["redirect_uri"][0] + "?" + urlencode({"code": code, "state": q["state"][0]}))
            self.end_headers()
            return
        result = {"/keys": {"keys": [jwk]}, "/.well-known/openid-configuration": discovery}.get(self.path)
        if result is None:
            self.send_error(404)
            return
        self.reply(result)

    def do_POST(self):
        p = parse_qs(self.rfile.read(int(self.headers["Content-Length"])).decode())
        q = codes.pop(p.get("code", [""])[0], None)
        if (self.path != "/token" or not q or p.get("client_secret") != ["synthetic-secret"]
                or p.get("redirect_uri") != q["redirect_uri"] or
                b64(hashlib.sha256(p["code_verifier"][0].encode()).digest()) != q["code_challenge"][0]):
            self.send_error(400)
            return
        token = signed(claims | {"sub": "synthetic-admin", "aud": "synthetic-client", "nonce": q["nonce"][0]})
        self.reply({"id_token": token, "access_token": "fixture", "token_type": "Bearer", "expires_in": 1800})

    def reply(self, result):
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
