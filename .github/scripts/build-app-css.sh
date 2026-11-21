#!/usr/bin/env bash
# Run from the repository root on Linux x64.
set -euo pipefail

version=v4.3.5
sha256=e47695cee3f0d90623a91f3939b08bdee610eb5ad99340adff9d4145b284d6cc
output="$(realpath -m "${1:?Usage: build-app-css.sh <output-file>}")"
cli="$(mktemp "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/tailwindcss.XXXXXXXX")"
trap 'rm -f -- "$cli"' EXIT

curl --fail --silent --show-error --location --output "$cli" \
  "https://github.com/tailwindlabs/tailwindcss/releases/download/$version/tailwindcss-linux-x64"
printf '%s  %s\n' "$sha256" "$cli" | sha256sum --check
chmod +x "$cli"

cd Client
"$cli" --input Styles/app.css --output "$output" --minify
