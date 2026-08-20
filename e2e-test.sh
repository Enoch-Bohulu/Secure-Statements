#!/usr/bin/env bash
#
# e2e-test.sh — Upload a PDF, then download it back. That's the whole test.
# Run the API first (see the README steps), then run:  ./e2e-test.sh
#
set -euo pipefail

# ---- Settings that match appsettings.json (don't change these) ----------------
API="http://localhost:5173"
JWT_KEY="local-dev-jwt-signing-key-change-me-at-least-32chars"
ISSUER="https://auth.local.securestatements"
AUDIENCE="secure-statements-api"
CUSTOMER="cust-001"

# ---- Tiny helper: make a signed JWT (header.payload.signature) ----------------
b64url() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }
mint_jwt() {                      # $1 = extra claims JSON (e.g. role)
  local now exp header payload signing sig
  now=$(date +%s); exp=$((now + 3600))
  header=$(printf '{"alg":"HS256","typ":"JWT"}' | b64url)
  payload=$(printf '{"sub":"%s","iss":"%s","aud":"%s","iat":%s,"exp":%s%s}' \
            "$CUSTOMER" "$ISSUER" "$AUDIENCE" "$now" "$exp" "$1" | b64url)
  signing="${header}.${payload}"
  sig=$(printf '%s' "$signing" | openssl dgst -sha256 -hmac "$JWT_KEY" -binary | b64url)
  printf '%s.%s' "$signing" "$sig"
}

ADMIN_TOKEN=$(mint_jwt ',"role":"statements-admin"')   # can upload
CUSTOMER_TOKEN=$(mint_jwt '')                           # can list + get link

echo "==> 1. Make a tiny sample PDF"
printf '%%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%%%EOF\n' > /tmp/sample.pdf

echo "==> 2. Upload it (as admin)"
UPLOAD=$(curl -s -X POST "$API/admin/statements" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -F "CustomerId=$CUSTOMER" -F "Period=2026-07" -F "File=@/tmp/sample.pdf;type=application/pdf")
echo "    $UPLOAD"
ID=$(printf '%s' "$UPLOAD" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')

echo "==> 3. List my statements (as customer)"
curl -s "$API/statements" -H "Authorization: Bearer $CUSTOMER_TOKEN"; echo

echo "==> 4. Ask for a download link"
LINK=$(curl -s -X POST "$API/statements/$ID/download-link" -H "Authorization: Bearer $CUSTOMER_TOKEN")
echo "    $LINK"
URL=$(printf '%s' "$LINK" | sed -n 's/.*"downloadUrl":"\([^"]*\)".*/\1/p')

echo "==> 5. Download the file back"
curl -s "$URL" -o /tmp/downloaded.pdf

echo "==> 6. Did we get the same file back?"
if cmp -s /tmp/sample.pdf /tmp/downloaded.pdf; then
  echo "    ✅ SUCCESS — uploaded and downloaded file match."
else
  echo "    ❌ FAIL — files differ."; exit 1
fi

