#!/usr/bin/env python3
import json
import sys
from pathlib import Path

MODE = sys.argv[1] if len(sys.argv) > 1 else "demo"
ROOT = Path(__file__).resolve().parents[4]
EVIDENCE = ROOT / "unity-hair-salon" / "Builds" / "PipelineEvidence"
browser = json.loads((EVIDENCE / "browser-check.json").read_text(encoding="utf-8"))
section = browser["demo" if MODE == "demo" else "assetLab"]
categories = ["resources", "proportion", "position", "direction", "shadow", "occlusion-depth", "character-size", "safe-area"]
screenshots = section.get("screenshots") or [section.get("screenshot")]
screenshots = [item for item in screenshots if item]
machine_ok = bool(section.get("loaded")) and not section.get("errors", [])
checks = []
for name in categories:
    if name == "resources":
        checks.append({
            "category": name,
            "status": "pass" if machine_ok else "fail",
            "issues": section.get("errors", [])
        })
    else:
        checks.append({"category": name, "status": "baseline-missing", "issues": []})
report = {
    "scene": MODE,
    "viewport": browser["viewport"],
    "machine": {
        "launched": bool(section.get("loaded")),
        "consoleErrors": section.get("errors", []),
        "screenshots": screenshots,
        "coreFlowPassed": section.get("coreFlowPassed")
    },
    "approvedReference": None,
    "checks": checks,
    "note": "机器检查已完成；未提供产品负责人批准的视觉基准时，不自动宣称视觉匹配。"
}
(EVIDENCE / "visual-qa-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps(report, ensure_ascii=False))
