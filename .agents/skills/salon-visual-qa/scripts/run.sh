#!/bin/zsh
set -euo pipefail

mode="${1:-demo}"
if [[ "$mode" != "demo" && "$mode" != "asset-lab" ]]; then
  print -u2 "用法：run.sh [demo|asset-lab]"
  exit 2
fi
repo_root="${0:A:h:h:h:h:h}"
unity_project="$repo_root/unity-hair-salon"
unity_bin="/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity"

"$unity_bin" -batchmode -nographics -quit \
  -projectPath "$unity_project" -executeMethod BuildScript.ValidatePipeline \
  -logFile "$unity_project/Builds/VisualQaValidate.log"
if [[ "$mode" == "demo" ]]; then
  build_method="BuildScript.BuildWebGLDemo"
else
  build_method="BuildScript.BuildWebGLAssetLab"
fi
"$unity_bin" -batchmode -nographics -quit \
  -projectPath "$unity_project" -executeMethod "$build_method" \
  -logFile "$unity_project/Builds/VisualQaBuild.log"
python3 "$repo_root/tools/browser-check.py" --mode "$mode"
python3 "${0:A:h}/write_report.py" "$mode"
