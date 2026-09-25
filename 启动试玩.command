#!/bin/zsh
set -euo pipefail
cd -- "$(dirname -- "$0")"
exec python3 tools/serve-salon.py --port 0 --open
