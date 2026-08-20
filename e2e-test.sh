#!/usr/bin/env bash
#
# End-to-end smoke test for the Secure Statements API.

set -uo pipefail

# Defaults line up with appsettings.json / .env so the tokens we mint below are accepted.
# Point at another environment by overriding, e.g. API=https://staging.example.com ./e2e-test.sh
API="${API:-http://localhost:5173}"
JWT_KEY="${JWT_KEY:-local-dev-jwt-signing-key-change-me-at-least-32chars}"
DOWNLOAD_KEY="${DOWNLOAD_KEY:-local-dev-download-token-signing-key-change-me-32chars}"
ISSUER="${JWT_ISSUER:-https://auth.local.securestatements}"
AUDIENCE="${JWT_AUDIENCE:-secure-statements-api}"

OWNER="cust-${RANDOM}"
OTHER="cust-${RANDOM}"

WORKDIR="$(mktemp -d)" || { echo "error: could not create a temp working directory"; exit 1; }
trap 'rm -rf "$WORKDIR"' EXIT

pass=0
fail=0

b64url() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }

mint_jwt() {
  local sub="$1" extra="${2:-}" now exp header payload signing
  now="$(date +%s)"
  exp="$((now + 3600))"
  header="$(printf '{"alg":"HS256","typ":"JWT"}' | b64url)"
  payload="$(printf '{"sub":"%s","iss":"%s","aud":"%s","iat":%s,"exp":%s%s}' \
    "$sub" "$ISSUER" "$AUDIENCE" "$now" "$exp" "$extra" | b64url)"
  signing="${header}.${payload}"
  printf '%s.%s' "$signing" \
    "$(printf '%s' "$signing" | openssl dgst -sha256 -hmac "$JWT_KEY" -binary | b64url)"
}

expired_download_token() {
  local statement_id="$1" customer="$2" expiry payload
  expiry="$(( $(date +%s) - 60 ))"
  payload="${statement_id}|${customer}|${expiry}"
  printf '%s.%s' \
    "$(printf '%s' "$payload" | b64url)" \
    "$(printf '%s' "$payload" | openssl dgst -sha256 -hmac "$DOWNLOAD_KEY" -binary | b64url)"
}

http_status() {
  local method="$1" url="$2"; shift 2
  curl -s -o /dev/null -w '%{http_code}' -X "$method" "$url" "$@"
}

http_body() {
  local out="$1" method="$2" url="$3"; shift 3
  curl -s -o "$out" -w '%{http_code}' -X "$method" "$url" "$@"
}

check_status() {
  if [[ "$3" == "$2" ]]; then
    printf 'PASS  %-53s [%s]\n' "$1" "$3"
    pass="$((pass + 1))"
  else
    printf 'FAIL  %-53s [expected %s, got %s]\n' "$1" "$2" "$3"
    fail="$((fail + 1))"
  fi
}

check_true() {
  if [[ "$2" == "ok" ]]; then
    printf 'PASS  %-53s\n' "$1"
    pass="$((pass + 1))"
  else
    printf 'FAIL  %-53s\n' "$1"
    fail="$((fail + 1))"
  fi
}

json_field() { sed -n "s/.*\"$2\":\"\([^\"]*\)\".*/\1/p" "$1"; }


if [[ "$(http_status GET "$API/health")" != "200" ]]; then
  echo "error: no healthy API at $API"
  echo "start it first:  docker compose up --build   (or: dotnet run --project src/SecureStatements.Api)"
  exit 1
fi

ADMIN_JWT="$(mint_jwt 'admin-user' ',"role":"statements-admin"')"
OWNER_JWT="$(mint_jwt "$OWNER")"
OTHER_JWT="$(mint_jwt "$OTHER")"

SAMPLE_PDF="$WORKDIR/sample.pdf"
NOT_PDF="$WORKDIR/notes.txt"
printf '%%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%%%EOF\n' > "$SAMPLE_PDF"
printf 'this is clearly not a pdf\n' > "$NOT_PDF"

echo "Target : $API"
echo "Owner  : $OWNER"
echo

echo "Health and auth"
check_status "health is public" 200 "$(http_status GET "$API/health")"
check_status "listing without a token is rejected" 401 "$(http_status GET "$API/statements")"
check_status "listing with a valid customer token is allowed" 200 \
  "$(http_status GET "$API/statements" -H "Authorization: Bearer $OWNER_JWT")"
check_status "upload as a non-admin is forbidden" 403 \
  "$(http_status POST "$API/admin/statements" \
      -H "Authorization: Bearer $OWNER_JWT" \
      -F "CustomerId=$OWNER" -F "Period=2026-07" \
      -F "File=@$SAMPLE_PDF;type=application/pdf")"
echo


echo "Upload validation"
check_status "a file that is not a PDF is rejected" 400 \
  "$(http_status POST "$API/admin/statements" \
      -H "Authorization: Bearer $ADMIN_JWT" \
      -F "CustomerId=$OWNER" -F "Period=2026-07" \
      -F "File=@$NOT_PDF;type=application/pdf")"
echo

echo "Happy path"
upload_body="$WORKDIR/upload.json"
check_status "admin uploads a valid PDF" 201 \
  "$(http_body "$upload_body" POST "$API/admin/statements" \
      -H "Authorization: Bearer $ADMIN_JWT" \
      -F "CustomerId=$OWNER" -F "Period=2026-07" \
      -F "File=@$SAMPLE_PDF;type=application/pdf")"

ID="$(json_field "$upload_body" id)"
check_true "the upload response includes a statement id" "$([[ -n "$ID" ]] && echo ok)"

list_body="$WORKDIR/list.json"
http_body "$list_body" GET "$API/statements" -H "Authorization: Bearer $OWNER_JWT" >/dev/null
check_true "the owner sees the statement in their list" \
  "$([[ -n "$ID" ]] && grep -q "$ID" "$list_body" && echo ok)"

check_status "another customer cannot get a link for it" 404 \
  "$(http_status POST "$API/statements/$ID/download-link" -H "Authorization: Bearer $OTHER_JWT")"

link_body="$WORKDIR/link.json"
check_status "the owner gets a download link" 200 \
  "$(http_body "$link_body" POST "$API/statements/$ID/download-link" \
      -H "Authorization: Bearer $OWNER_JWT")"
URL="$(json_field "$link_body" downloadUrl)"

downloaded="$WORKDIR/downloaded.pdf"
check_status "the link redeems successfully" 200 \
  "$(http_body "$downloaded" GET "${URL:-$API/download/missing}")"
check_true "the downloaded bytes match the uploaded file" \
  "$(cmp -s "$SAMPLE_PDF" "$downloaded" && echo ok)"
echo

echo "Download token security"
check_status "a garbage token is rejected" 401 \
  "$(http_status GET "$API/download/not-a-real-token")"
check_status "a well-formed but wrongly signed token is rejected" 401 \
  "$(http_status GET "$API/download/YWJj.ZGVm")"
expired="$(expired_download_token "$(printf '%s' "${ID:-}" | tr -d '-')" "$OWNER")"
check_status "a correctly signed but expired token is rejected" 401 \
  "$(http_status GET "$API/download/$expired")"
echo

echo "-------------------------------------------------------"
printf 'Passed: %d   Failed: %d\n' "$pass" "$fail"
[[ "$fail" -eq 0 ]]