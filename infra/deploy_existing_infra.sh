#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TMP_DIR="$(mktemp -d /tmp/classfinder-deploy-XXXXXX)"
trap 'rm -rf "$TMP_DIR"' EXIT

# Defaults target the currently active classfinder Azure environment.
SUBSCRIPTION_ID="${SUBSCRIPTION_ID:-0269239e-7a1f-48ed-9585-b427b19eac6e}"
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-classfinder-0990a9}"
CONTAINER_APP_NAME="${CONTAINER_APP_NAME:-ca-classfinder-api-0990a9}"
ACR_NAME="${ACR_NAME:-cfacr0990a9}"
BACKEND_IMAGE_REPO="${BACKEND_IMAGE_REPO:-backend}"
BACKEND_IMAGE_TAG="${BACKEND_IMAGE_TAG:-latest}"
BACKEND_DOCKERFILE="${BACKEND_DOCKERFILE:-$ROOT_DIR/backend/Dockerfile}"
BACKEND_CONTEXT_DIR="${BACKEND_CONTEXT_DIR:-$ROOT_DIR/backend}"
FRONTEND_DIR="${FRONTEND_DIR:-$ROOT_DIR/frontend/ui-classfinder}"
FRONTEND_STORAGE_ACCOUNT="${FRONTEND_STORAGE_ACCOUNT:-classfinderui0990a9}"
RUN_NPM_CI="${RUN_NPM_CI:-0}"
SMOKE_CYCLES="${SMOKE_CYCLES:-5}"
SMOKE_STUDENT_ID="${SMOKE_STUDENT_ID:-student-123}"
SMOKE_CLASS_TOKEN="${SMOKE_CLASS_TOKEN:-CSCE331-04}"

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Missing required command: $1" >&2
    exit 1
  fi
}

require_cmd az
require_cmd npm
require_cmd curl
require_cmd jq

echo "Using subscription: $SUBSCRIPTION_ID"
echo "Resource group: $RESOURCE_GROUP"
echo "Container app: $CONTAINER_APP_NAME"
echo "ACR: $ACR_NAME"
echo "Frontend storage: $FRONTEND_STORAGE_ACCOUNT"

az account set --subscription "$SUBSCRIPTION_ID"

IMAGE_REF="${ACR_NAME}.azurecr.io/${BACKEND_IMAGE_REPO}:${BACKEND_IMAGE_TAG}"

echo "Building and pushing backend image: $IMAGE_REF"
az acr build \
  --subscription "$SUBSCRIPTION_ID" \
  --registry "$ACR_NAME" \
  --image "${BACKEND_IMAGE_REPO}:${BACKEND_IMAGE_TAG}" \
  --file "$BACKEND_DOCKERFILE" \
  "$BACKEND_CONTEXT_DIR" >/dev/null

echo "Updating container app revision..."
az containerapp update \
  --subscription "$SUBSCRIPTION_ID" \
  -g "$RESOURCE_GROUP" \
  -n "$CONTAINER_APP_NAME" \
  --image "$IMAGE_REF" >/dev/null

API_HOST="$(az containerapp show \
  --subscription "$SUBSCRIPTION_ID" \
  -g "$RESOURCE_GROUP" \
  -n "$CONTAINER_APP_NAME" \
  --query properties.configuration.ingress.fqdn \
  -o tsv)"

if [[ -z "$API_HOST" ]]; then
  echo "Unable to resolve container app ingress host for $CONTAINER_APP_NAME" >&2
  exit 1
fi

API_URL="https://${API_HOST}"

echo "Building frontend with API base URL: $API_URL"
if [[ ! -d "$FRONTEND_DIR" ]]; then
  echo "Frontend directory does not exist: $FRONTEND_DIR" >&2
  exit 1
fi

pushd "$FRONTEND_DIR" >/dev/null
if [[ "$RUN_NPM_CI" == "1" ]]; then
  npm ci >/dev/null
fi
VITE_API_BASE_URL="$API_URL" npm run build >/dev/null
popd >/dev/null

echo "Configuring static website and uploading frontend assets..."
az storage blob service-properties update \
  --subscription "$SUBSCRIPTION_ID" \
  --account-name "$FRONTEND_STORAGE_ACCOUNT" \
  --auth-mode login \
  --static-website \
  --index-document index.html \
  --404-document index.html >/dev/null

STORAGE_KEY="$(az storage account keys list \
  --subscription "$SUBSCRIPTION_ID" \
  -g "$RESOURCE_GROUP" \
  -n "$FRONTEND_STORAGE_ACCOUNT" \
  --query '[0].value' \
  -o tsv)"

az storage blob upload-batch \
  --subscription "$SUBSCRIPTION_ID" \
  --account-name "$FRONTEND_STORAGE_ACCOUNT" \
  --account-key "$STORAGE_KEY" \
  -s "$FRONTEND_DIR/dist" \
  -d '$web' \
  --overwrite >/dev/null

FRONTEND_URL="$(az storage account show \
  --subscription "$SUBSCRIPTION_ID" \
  -g "$RESOURCE_GROUP" \
  -n "$FRONTEND_STORAGE_ACCOUNT" \
  --query primaryEndpoints.web \
  -o tsv)"

status_check() {
  local expected="$1"
  local actual="$2"
  local msg="$3"
  if [[ "$actual" != "$expected" ]]; then
    echo "Smoke test failed: $msg (expected $expected, got $actual)" >&2
    exit 1
  fi
}

contains_class() {
  local file="$1"
  local token="$2"
  jq -r --arg token "$token" 'any(.scheduledClasses[]?; .classId == $token)' "$file"
}

echo "Running smoke tests..."
READ_STATUS="$(curl -sS -o "$TMP_DIR/read_classes.json" -w "%{http_code}" \
  "$API_URL/api/classes?page=1&pageSize=1")"
status_check "200" "$READ_STATUS" "GET /api/classes"

STATE_BEFORE_STATUS="$(curl -sS -o "$TMP_DIR/state_before_finalize.json" -w "%{http_code}" \
  "$API_URL/api/students/$SMOKE_STUDENT_ID/schedule/state")"
status_check "200" "$STATE_BEFORE_STATUS" "GET schedule/state before finalize"

jq '{
  scheduledClasses: [
    .scheduledClasses[]? | {
      sectionId: .sectionId,
      classId: .classId
    }
  ]
}' "$TMP_DIR/state_before_finalize.json" > "$TMP_DIR/finalize_payload.json"

FINALIZE_STATUS="$(curl -sS -o "$TMP_DIR/finalize_response.json" -w "%{http_code}" -X POST \
  "$API_URL/api/students/$SMOKE_STUDENT_ID/schedule/finalize" \
  -H 'Content-Type: application/json' \
  --data @"$TMP_DIR/finalize_payload.json")"
status_check "200" "$FINALIZE_STATUS" "POST schedule/finalize"

STATE_AFTER_FINALIZE_STATUS="$(curl -sS -o "$TMP_DIR/state_after_finalize.json" -w "%{http_code}" \
  "$API_URL/api/students/$SMOKE_STUDENT_ID/schedule/state")"
status_check "200" "$STATE_AFTER_FINALIZE_STATUS" "GET schedule/state after finalize"

if ! diff -q \
  <(jq -S '.scheduledClasses | map({sectionId, classId}) | sort_by(.classId, .sectionId)' "$TMP_DIR/finalize_response.json") \
  <(jq -S '.scheduledClasses | map({sectionId, classId}) | sort_by(.classId, .sectionId)' "$TMP_DIR/state_after_finalize.json") \
  >/dev/null; then
  echo "Smoke test failed: finalized schedule mismatch with persisted schedule/state" >&2
  exit 1
fi

for i in $(seq 1 "$SMOKE_CYCLES"); do
  PRE_DELETE_STATUS="$(curl -sS -o "$TMP_DIR/pre_delete_${i}.json" -w "%{http_code}" -X DELETE \
    "$API_URL/api/students/$SMOKE_STUDENT_ID/schedule/$SMOKE_CLASS_TOKEN")"
  status_check "200" "$PRE_DELETE_STATUS" "pre-clean DELETE cycle $i"

  ADD_STATUS="$(curl -sS -o "$TMP_DIR/add_${i}.json" -w "%{http_code}" -X POST \
    "$API_URL/api/students/$SMOKE_STUDENT_ID/schedule" \
    -H 'Content-Type: application/json' \
    -d "{\"classId\":\"$SMOKE_CLASS_TOKEN\"}")"
  status_check "200" "$ADD_STATUS" "POST add class cycle $i"

  STATE_ADD_STATUS="$(curl -sS -o "$TMP_DIR/state_add_${i}.json" -w "%{http_code}" \
    "$API_URL/api/students/$SMOKE_STUDENT_ID/schedule/state")"
  status_check "200" "$STATE_ADD_STATUS" "GET schedule/state after add cycle $i"

  HAS_CLASS_AFTER_ADD="$(contains_class "$TMP_DIR/state_add_${i}.json" "$SMOKE_CLASS_TOKEN")"
  if [[ "$HAS_CLASS_AFTER_ADD" != "true" ]]; then
    echo "Smoke test failed: class $SMOKE_CLASS_TOKEN missing after add in cycle $i" >&2
    exit 1
  fi

  REMOVE_STATUS="$(curl -sS -o "$TMP_DIR/remove_${i}.json" -w "%{http_code}" -X DELETE \
    "$API_URL/api/students/$SMOKE_STUDENT_ID/schedule/$SMOKE_CLASS_TOKEN")"
  status_check "200" "$REMOVE_STATUS" "DELETE remove class cycle $i"

  STATE_REMOVE_STATUS="$(curl -sS -o "$TMP_DIR/state_remove_${i}.json" -w "%{http_code}" \
    "$API_URL/api/students/$SMOKE_STUDENT_ID/schedule/state")"
  status_check "200" "$STATE_REMOVE_STATUS" "GET schedule/state after remove cycle $i"

  HAS_CLASS_AFTER_REMOVE="$(contains_class "$TMP_DIR/state_remove_${i}.json" "$SMOKE_CLASS_TOKEN")"
  if [[ "$HAS_CLASS_AFTER_REMOVE" != "false" ]]; then
    echo "Smoke test failed: class $SMOKE_CLASS_TOKEN still present after remove in cycle $i" >&2
    exit 1
  fi

  echo "Smoke cycle $i/$SMOKE_CYCLES passed."
done

FRONTEND_STATUS="$(curl -sS -o "$TMP_DIR/frontend.html" -w "%{http_code}" "$FRONTEND_URL")"
status_check "200" "$FRONTEND_STATUS" "frontend URL"

echo
echo "Deploy completed successfully."
echo "API_URL=$API_URL"
echo "FRONTEND_URL=$FRONTEND_URL"
echo "SMOKE_CYCLES=$SMOKE_CYCLES"
