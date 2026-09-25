#!/bin/zsh
set -euo pipefail

repo_root="${0:A:h:h:h:h:h}"
unity_project="$repo_root/unity-hair-salon"
unity_bin="/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity"

file_arg=""
id_arg=""
revalidate_mode="false"
args=("$@")
for (( i = 1; i <= ${#args}; i++ )); do
  if [[ "${args[$i]}" == "--revalidate" ]]; then
    revalidate_mode="true"
  fi
  if [[ "${args[$i]}" == "--file" && $(( i + 1 )) -le ${#args} ]]; then
    file_arg="${args[$(( i + 1 ))]}"
  fi
  if [[ "${args[$i]}" == "--id" && $(( i + 1 )) -le ${#args} ]]; then
    id_arg="${args[$(( i + 1 ))]}"
  fi
done
if [[ -z "$id_arg" || ( "$revalidate_mode" != "true" && -z "$file_arg" ) ]]; then
  print -u2 "用法：ingest.sh --file <assets/inbox 中的 PNG 文件名> --id <稳定ID> --type <类型> --config <资产配置 JSON>"
  print -u2 "或：ingest.sh --revalidate --id <已存在的 candidate 稳定ID>"
  exit 2
fi
if [[ "$revalidate_mode" != "true" && "${file_arg:l}" != *.png ]]; then
  print -u2 "salon-asset-ingest 只接入 PNG：$file_arg"
  exit 2
fi

if [[ "$revalidate_mode" != "true" ]]; then
  node "$repo_root/tools/asset-pipeline.mjs" import "$@"
fi
node --test "$repo_root/tests/asset-pipeline.test.mjs"
"$unity_bin" -batchmode -nographics -quit \
  -projectPath "$unity_project" -executeMethod Production2D5TexturePolicy.ApplyAllBatch \
  -logFile "$unity_project/Builds/AssetIngestTexturePolicy.log"
"$unity_bin" -batchmode -nographics -quit \
  -projectPath "$unity_project" -executeMethod BuildScript.ValidatePipeline \
  -logFile "$unity_project/Builds/AssetIngestValidate.log"
"$unity_bin" -batchmode -nographics -quit \
  -projectPath "$unity_project" -executeMethod BuildScript.BuildWebGLAssetLab \
  -logFile "$unity_project/Builds/AssetIngestLabBuild.log"
python3 "$repo_root/tools/browser-check.py" --mode asset-lab --asset-id "$id_arg"
