#!/bin/zsh
set -euo pipefail

if (( $# != 1 )); then
  print -u2 "用法：inspect-existing.sh <仓库内图片路径>"
  exit 2
fi
repo_root="${0:A:h:h:h:h:h}"
node "$repo_root/tools/asset-pipeline.mjs" inspect --file "$1"
