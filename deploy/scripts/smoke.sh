#!/usr/bin/env bash
# Smoke checks against a deployed Kubix environment.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
if [[ -f "$ROOT/deploy/.out/urls.env" ]]; then
  # shellcheck disable=SC1091
  source "$ROOT/deploy/.out/urls.env"
fi

API_URL="${API_URL:?Set API_URL or run deploy first}"
WEB_URL="${WEB_URL:-}"

echo "==> Health API $API_URL"
API_CODE="$(curl -s -o /tmp/kubix-health.json -w "%{http_code}" "$API_URL/api/v1/health" || echo 000)"
echo "GET /api/v1/health => $API_CODE"
[[ "$API_CODE" == "200" ]] || { cat /tmp/kubix-health.json 2>/dev/null || true; echo "API smoke failed"; exit 1; }

if [[ -n "$WEB_URL" ]]; then
  echo "==> Web $WEB_URL"
  for route in / /admin/usuarios /super/universidades /utn-logo.png; do
    WCODE="$(curl -s -o /dev/null -w "%{http_code}" "$WEB_URL$route")"
    echo "Web $route => $WCODE"
    [[ "$WCODE" == "200" ]] || { echo "Web smoke failed for $route"; exit 1; }
  done

  API_GUARD_CODE="$(curl -s -o /dev/null -w "%{http_code}" "$WEB_URL/api/admin/users")"
  echo "API /api/admin/users (sin token) => $API_GUARD_CODE"
  [[ "$API_GUARD_CODE" == "401" ]] || {
    echo "API prefix/routing smoke failed"
    exit 1
  }
fi

echo "Smoke OK"
