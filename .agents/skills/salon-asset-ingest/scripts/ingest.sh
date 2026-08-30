#!/bin/zsh
set -euo pipefail

repo_root="${0:A:h:h:h:h:h}"
unity_project="$repo_root/unity-hair-salon"
unity_bin="/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity"

file_arg=""
args=("$@")
for (( i = 1; i <= ${#args}; i++ )); do
  if [[ "${args[$i]}" == "--file" && $(( i + 1 )) -le ${#args} ]]; then
    file_arg="${args[$(( i + 1 ))]}"
    break
  fi
done
if [[ -z "$file_arg" ]]; then
  print -u2 "用法：ingest.sh --file <assets/inbox 中的 PNG 文件名> --id <稳定ID> --type <类型>"
  exit 2
fi
if [[ "${file_arg:l}" != *.png ]]; then
  print -u2 "salon-asset-ingest 只接入 PNG：$file_arg"
  exit 2
fi

node "$repo_root/tools/asset-pipeline.mjs" import "$@"
node --test "$repo_root/tests/asset-pipeline.test.mjs"
"$unity_bin" -batchmode -nographics -quit \
  -projectPath "$unity_project" -executeMethod BuildScript.ValidatePipeline \
  -logFile "$unity_project/Builds/AssetIngestValidate.log"
"$unity_bin" -batchmode -nographics -quit \
  -projectPath "$unity_project" -executeMethod BuildScript.BuildWebGLAssetLab \
  -logFile "$unity_project/Builds/AssetIngestLabBuild.log"
python3 "$repo_root/tools/browser-check.py" --mode asset-lab
