#!/usr/bin/env bash
set -euo pipefail
: "${HEALTHSYNCY_DOMAIN:?}" "${IMAGE_PREFIX:?}" "${IMAGE_TAG:?}"
root=$(cd "$(dirname "$0")/.." && pwd)
output=$(mktemp -d)
trap 'rm -rf "$output"' EXIT
render_args=(--domain "$HEALTHSYNCY_DOMAIN" --registry "$IMAGE_PREFIX" --tag "$IMAGE_TAG" --output "$output")
case "${HEALTHSYNCY_DEPLOYMENT_PROFILE:-standard}" in
  standard) ;;
  shared-node)
    provisioner=$(kubectl get storageclass local-path -o jsonpath='{.provisioner}')
    if [[ "$provisioner" != rancher.io/local-path ]]; then
      echo "The shared-node profile requires the Rancher local-path storage class." >&2
      exit 1
    fi
    render_args+=(--shared-node)
    ;;
  *) echo "Unknown HEALTHSYNCY_DEPLOYMENT_PROFILE" >&2; exit 1 ;;
esac
python3 "$root/scripts/render.py" "${render_args[@]}"
for secret in mongo-init-secrets sync-config admin-config connector-config healthsyncy-tls; do
  kubectl -n healthsyncy get secret "$secret" -o name >/dev/null
done
if [[ "${HEALTHSYNCY_DEPLOYMENT_PROFILE:-standard}" == shared-node ]]; then
  kubectl apply -k "$output" -l app.kubernetes.io/component=isolation
fi
kubectl apply -k "$output"
for deployment in mongo sync connector admin; do
  kubectl -n healthsyncy rollout status "deployment/$deployment" --timeout=5m
done
