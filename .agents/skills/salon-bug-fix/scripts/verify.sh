#!/bin/zsh
set -euo pipefail

filter=""
browser=0
while (( $# > 0 )); do
  case "$1" in
    --filter) filter="$2"; shift 2 ;;
    --browser) browser=1; shift ;;
    *) print -u2 "未知参数：$1"; exit 2 ;;
  esac
done
if [[ -z "$filter" ]]; then
  print -u2 "用法：verify.sh --filter <Unity测试名> [--browser]"
  exit 2
fi

repo_root="${0:A:h:h:h:h:h}"
unity_project="$repo_root/unity-hair-salon"
unity_bin="/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity"
result="$unity_project/Builds/BugFixFocused.xml"

"$unity_bin" -batchmode -nographics -projectPath "$unity_project" \
  -runTests -testPlatform EditMode -testFilter "$filter" \
  -testResults "$result" -logFile "$unity_project/Builds/BugFixFocused.log"
python3 - "$result" <<'PY'
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
if root.attrib.get("failed") != "0" or root.attrib.get("result") != "Passed":
    raise SystemExit("专项回归测试失败：" + str(root.attrib))
print("专项回归测试通过：" + str(root.attrib))
PY

if (( browser )); then
  "$unity_bin" -batchmode -nographics -quit -projectPath "$unity_project" \
    -executeMethod BuildScript.BuildWebGLDemo -logFile "$unity_project/Builds/BugFixWebGLBuild.log"
  python3 "$repo_root/tools/browser-check.py" --mode demo
fi
