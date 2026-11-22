#!/usr/bin/env bash
set -euo pipefail
umask 077
: "${HEALTHSYNKI_DOMAIN:?}" "${OAUTH_ISSUER:?}" "${OWNER_SUBJECT:?}" "${ADMIN_SUBJECT:?}" "${ADMIN_CLIENT_ID:?}" "${ADMIN_CLIENT_SECRET:?}"
: "${DEVICE_TOKEN_FILE:?Choose a private path for the generated iPhone token}"
bundle="$DEVICE_TOKEN_FILE.bootstrap.json"
[[ ! -e "$DEVICE_TOKEN_FILE" && ! -e "$bundle" ]] || { echo "Device token or bootstrap backup already exists." >&2; exit 1; }
dir=$(mktemp -d)
trap 'rm -rf "$dir"' EXIT
kubectl create namespace healthsynki --dry-run=client -o yaml | kubectl apply -f -
k=(kubectl -n healthsynki)
for name in mongo-init-secrets sync-config connector-config admin-config; do
  existing=$("${k[@]}" get secret "$name" --ignore-not-found -o name)
  [[ -z "$existing" ]] || { echo "Existing secrets found. This is a first-install command." >&2; exit 1; }
done
mkdir -p "$dir"/{mongo-init-secrets,sync-config,connector-config,admin-config}
put() { printf '%s' "$3" > "$dir/$1/$2"; }
put mongo-init-secrets MONGO_INITDB_ROOT_USERNAME root
put mongo-init-secrets MONGO_INITDB_ROOT_PASSWORD "$(openssl rand -hex 32)"
for role in sync connector admin; do
  password=$(openssl rand -hex 32)
  put mongo-init-secrets "MONGO_${role^^}" "$password"
  put "$role-config" MongoUri "mongodb://$role:$password@mongo:27017/activity?authSource=activity"
done
device=$(openssl rand -hex 32)
(set -C; printf '%s\n' "$device" > "$DEVICE_TOKEN_FILE")
put sync-config UploadTokenSha256 "$(printf '%s' "$device" | openssl dgst -sha256 -r | cut -d' ' -f1)"
put connector-config OAuthIssuer "$OAUTH_ISSUER"
put connector-config OwnerSubject "$OWNER_SUBJECT"
put connector-config McpResource "https://connector.$HEALTHSYNKI_DOMAIN/mcp"
put admin-config OAuthIssuer "$OAUTH_ISSUER"
put admin-config AdminSubject "$ADMIN_SUBJECT"
put admin-config AdminOrigin "https://admin.$HEALTHSYNKI_DOMAIN"
put admin-config AdminClientId "$ADMIN_CLIENT_ID"
put admin-config AdminClientSecret "$ADMIN_CLIENT_SECRET"
put admin-config ReportingZone "${REPORTING_ZONE:-UTC}"
# A single List lets the operator resume a partial first install with the same credentials.
for name in mongo-init-secrets sync-config connector-config admin-config; do
  "${k[@]}" create secret generic "$name" --from-file="$dir/$name" --dry-run=client -o json > "$dir/$name.json"
done
(set -C; python3 -c 'import glob,json,sys; print(json.dumps(dict(kind="List",apiVersion="v1",items=[json.load(open(p)) for p in glob.glob(sys.argv[1]+"/*.json")])) )' "$dir" > "$bundle")
"${k[@]}" create -f "$bundle"
printf 'Device token and private bootstrap backup saved at your requested paths. Store them in your secret manager.\n'
