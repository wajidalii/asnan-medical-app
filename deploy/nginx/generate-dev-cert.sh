#!/usr/bin/env bash
# Generates a self-signed TLS cert for the local/dev docker-compose stack.
# Not for staging/production use — see DEPLOYMENT.md's TLS section for
# where a real certificate belongs there.
set -euo pipefail

cert_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/certs"
mkdir -p "$cert_dir"

openssl req -x509 -nodes -newkey rsa:2048 \
  -keyout "$cert_dir/dev.key" \
  -out "$cert_dir/dev.crt" \
  -days 365 \
  -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

echo "Wrote $cert_dir/dev.crt and $cert_dir/dev.key"
