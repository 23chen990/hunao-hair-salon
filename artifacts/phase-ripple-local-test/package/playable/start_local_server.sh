#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

if ! command -v python3 >/dev/null 2>&1; then
  echo "未找到 python3。请先安装 Python 3，或使用其他仅绑定本机的静态 HTTP 服务。" >&2
  exit 1
fi

echo "《相位涟漪》本地服务将只监听本机：http://127.0.0.1:4188/"
echo "请手动在浏览器打开该地址；关闭服务请按 Control-C。"
exec python3 -m http.server 4188 --bind 127.0.0.1 --directory "$SCRIPT_DIR"
