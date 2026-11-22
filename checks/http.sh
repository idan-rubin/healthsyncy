#!/usr/bin/env bash
set -euo pipefail
work=$(mktemp -d)
pids=()
trap 'for pid in "${pids[@]}"; do kill "$pid" 2>/dev/null || true; done; rm -rf "$work"' EXIT
export MongoUri="${TEST_MONGO_URI:?}" OAuthIssuer=https://localhost:5510 OwnerSubject=synthetic-owner
export McpResource=https://synthetic.invalid/mcp AdminOrigin=https://admin.synthetic.invalid
export AdminSubject=synthetic-admin AdminClientId=synthetic-client AdminClientSecret=synthetic-secret
export KeyDirectory="$work/keys"
openssl req -x509 -newkey rsa:2048 -nodes -keyout "$work/issuer.key" -out "$work/issuer.crt" \
  -days 1 -subj '/CN=localhost' -addext 'subjectAltName=DNS:localhost' -addext 'basicConstraints=critical,CA:TRUE' 2>/dev/null
export SSL_CERT_FILE="$work/issuer.crt"
python3 checks/issuer.py "$work" >"$work/issuer.log" 2>&1 &
pids+=("$!")
for i in {1..30}; do curl -fsS --cacert "$SSL_CERT_FILE" "$OAuthIssuer/keys" >/dev/null 2>&1 && break; sleep 1; done
curl -fsS --cacert "$SSL_CERT_FILE" "$OAuthIssuer/keys" >/dev/null
token=$(openssl rand -hex 32)
export UploadTokenSha256=$(printf %s "$token" | openssl dgst -sha256 -r | cut -d' ' -f1)
port=5500
for role in Sync Connector Admin; do
  port=$((port + 1))
  role_uri="${role^^}_MONGO_URI"
  MongoUri="${!role_uri:-$MongoUri}" ASPNETCORE_URLS="http://127.0.0.1:$port" dotnet "src/Services/HealthSynki.$role/HealthSynki.$role.EntryPoint/bin/Release/net11.0/HealthSynki.$role.EntryPoint.dll" >"$work/$role.log" 2>&1 &
  pids+=("$!")
  for i in {1..50}; do curl -fsS "http://127.0.0.1:$port/readyz" >/dev/null 2>&1 && break; sleep 1; done
  curl -fsS "http://127.0.0.1:$port/readyz" || { cat "$work/$role.log"; exit 1; }
done
expect() {
  local code=$1; shift
  actual=$(curl -sS -o "$work/body" -w '%{http_code}' "$@")
  [[ $actual == "$code" ]] || { echo "Expected HTTP $code; got $actual"; exit 1; }
}
date=$(date -u +%F)
now=$(date -u +%FT%TZ)
url="http://127.0.0.1:5501/api/v1/days/$date"
data='{"timeZone":"UTC","capturedAt":"'$now'","totals":{"steps":123,"activeKcal":20}}'
expect 401 -X PUT "$url" --json "$data"
expect 200 -X PUT "$url" -H "Authorization: Bearer $token" --json "$data"
expect 200 -X PUT "$url" -H "Authorization: Bearer $token" --json "$data"
python3 -c 'import json,sys; assert json.load(open(sys.argv[1]))["status"]=="Duplicate"' "$work/body"
expect 400 -X PUT "$url" -H "Authorization: Bearer $token" --json '{"timeZone":"bad"}'
expect 401 -X POST http://127.0.0.1:5502/mcp
expect 401 "http://127.0.0.1:5502/api/v1/activity/$date?timeZone=UTC"
expect 200 http://127.0.0.1:5502/.well-known/oauth-protected-resource/mcp
read_url="http://127.0.0.1:5502/api/v1/activity/$date?timeZone=UTC"
auth_header() { printf 'Authorization: Bearer %s' "$(cat "$work/$1.jwt")"; }
for fixture in wrong-user wrong-scope; do
  expect 403 -H "$(auth_header "$fixture")" "$read_url"
done
for fixture in wrong-aud expired; do
  expect 401 -H "$(auth_header "$fixture")" "$read_url"
done
expect 200 -H "$(auth_header valid)" "$read_url"
python3 -c 'import json,sys; assert json.load(open(sys.argv[1]))["totals"]["steps"] == 123' "$work/body"
expect 302 http://127.0.0.1:5503/
mcp="http://127.0.0.1:5502/mcp"
mcp_headers=(-H "$(auth_header valid)" -H 'Accept: application/json, text/event-stream' -H 'MCP-Protocol-Version: 2025-11-25')
expect 200 "${mcp_headers[@]}" "$mcp" --json '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"smoke","version":"1.0"}}}'
expect 200 "${mcp_headers[@]}" "$mcp" --json '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
python3 checks/mcp_response.py list "$work/body"
expect 200 "${mcp_headers[@]}" "$mcp" --json '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_activity_day","arguments":{"day":"'$date'","timeZone":"UTC"}}}'
python3 checks/mcp_response.py day "$work/body"
