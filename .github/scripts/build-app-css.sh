#!/usr/bin/env bash
# Builds Client/wwwroot/css/app.css from Client/Styles/app.css with the standalone Tailwind CLI, checked against its
# SHA-256, into the file given as the only argument:
#   .github/scripts/build-app-css.sh <output file>
# Needs TAILWIND_VERSION and TAILWIND_SHA256 (set in the workflow). Run from the repository root.
set -euo pipefail

output="$(realpath -m "$1")"
cli="${RUNNER_TEMP:-/tmp}/tailwindcss-$TAILWIND_VERSION"

if [ ! -x "$cli" ]; then
  curl --fail --silent --show-error --location --output "$cli" \
    "https://github.com/tailwindlabs/tailwindcss/releases/download/$TAILWIND_VERSION/tailwindcss-linux-x64"
  echo "$TAILWIND_SHA256  $cli" | sha256sum --check --status
  chmod +x "$cli"
fi

cd Client
"$cli" --input Styles/app.css --output "$output" --minify
