#!/usr/bin/env bash
set -euo pipefail
: "${HEALTHSYNKI_DOMAIN:?}" "${IMAGE_PREFIX:?}" "${IMAGE_TAG:?}"
root=$(cd "$(dirname "$0")/.." && pwd)
output=$(mktemp -d)
trap 'rm -rf "$output"' EXIT
python3 "$root/scripts/render.py" --domain "$HEALTHSYNKI_DOMAIN" --registry "$IMAGE_PREFIX" --tag "$IMAGE_TAG" --output "$output"
for secret in mongo-init-secrets sync-config admin-config connector-config healthsynki-tls; do
  kubectl -n healthsynki get secret "$secret" -o name >/dev/null
done
kubectl apply -k "$output"
for deployment in mongo sync connector admin; do
  kubectl -n healthsynki rollout status "deployment/$deployment" --timeout=5m
done
