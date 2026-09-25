#!/bin/zsh
set -euo pipefail

project_root="${0:A:h:h}"
unity_project="$project_root/unity-hair-salon"
unity_bin="/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity"

node --test "$project_root/tests/asset-pipeline.test.mjs"
python3 "$project_root/tools/check-ui-font.py"

"$unity_bin" -batchmode -nographics \
  -projectPath "$unity_project" \
  -runTests -testPlatform EditMode \
  -testResults "$unity_project/Builds/PipelineEditMode.xml" \
  -logFile "$unity_project/Builds/PipelineEditMode.log"

python3 - "$unity_project/Builds/PipelineEditMode.xml" <<'PY'
import sys
import xml.etree.ElementTree as ElementTree

root = ElementTree.parse(sys.argv[1]).getroot()
if root.attrib.get("result") != "Passed" or root.attrib.get("failed") != "0":
    raise SystemExit("Unity EditMode XML reports failure: " + str(root.attrib))
print("Unity EditMode XML verified: " + str(root.attrib))
PY

"$unity_bin" -batchmode -nographics -quit \
  -projectPath "$unity_project" \
  -executeMethod BuildScript.ValidatePipeline \
  -logFile "$unity_project/Builds/PipelineValidate.log"

"$unity_bin" -batchmode -nographics -quit \
  -projectPath "$unity_project" \
  -executeMethod BuildScript.BuildWebGLDemo \
  -logFile "$unity_project/Builds/WebGLDemoBuild.log"

"$unity_bin" -batchmode -nographics -quit \
  -projectPath "$unity_project" \
  -executeMethod BuildScript.BuildWebGLAssetLab \
  -logFile "$unity_project/Builds/WebGLAssetLabBuild.log"

"$unity_bin" -batchmode -nographics -quit \
  -projectPath "$unity_project" \
  -executeMethod BuildScript.BuildWebGLCandidate \
  -logFile "$unity_project/Builds/WebGLCandidateBuild.log"

"$unity_bin" -batchmode -nographics -quit \
  -projectPath "$unity_project" \
  -executeMethod BuildScript.BuildWebGLReferenceVisual \
  -logFile "$unity_project/Builds/WebGLReferenceVisualBuild.log"

python3 "$project_root/tools/browser-check.py"
