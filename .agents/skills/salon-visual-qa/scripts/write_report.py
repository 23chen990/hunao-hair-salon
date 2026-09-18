#!/usr/bin/env python3
import json
import sys
from pathlib import Path

MODE = sys.argv[1] if len(sys.argv) > 1 else "demo"
ROOT = Path(__file__).resolve().parents[4]
EVIDENCE = ROOT / "unity-hair-salon" / "Builds" / "PipelineEvidence"
mode_browser = EVIDENCE / f"browser-check-{MODE}.json"
all_browser = EVIDENCE / "browser-check-all.json"
browser_path = mode_browser if mode_browser.exists() else all_browser if all_browser.exists() else EVIDENCE / "browser-check.json"
browser = json.loads(browser_path.read_text(encoding="utf-8"))
section = browser["demo" if MODE == "demo" else "candidate" if MODE == "candidate" else "reference" if MODE == "reference" else "assetLab"]
categories = ["resources", "proportion", "position", "direction", "shadow", "occlusion-depth", "character-size", "safe-area", "stability"]
washAreaEvidence = section.get("washAreaEvidence", {})
uiEvidence = section.get("uiEvidence", {})
screenshots = section.get("screenshots") or [section.get("screenshot"), section.get("debugScreenshot")]
if uiEvidence:
    screenshots.extend(uiEvidence.get(key) for key in ("opening", "activeService"))
if washAreaEvidence:
    screenshots.extend(washAreaEvidence.get(key) for key in (
        "A_approvedReferenceCrop", "B_formalDemoBefore", "C_formalDemoAfter", "D_playerWashContext"))
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
    elif (MODE == "reference" and section.get("approvedReference")) or (MODE == "demo" and washAreaEvidence):
        checks.append({"category": name, "status": "manual-review", "issues": []})
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
    "approvedReference": section.get("approvedReference") or washAreaEvidence.get("A_approvedReferenceCrop"),
    "washAreaEvidence": washAreaEvidence,
    "uiEvidence": uiEvidence,
    "checks": checks,
    "note": ("机器检查已完成；HairSalonDemo 洗发区已按 A/B/C/D 输出，视觉接近程度仍由产品负责人确认。"
             if MODE == "demo" and washAreaEvidence else
             "机器检查已完成；Reference Scene 仅作为技术检查环境，不承担正式游戏视觉批准。"
             if MODE == "reference" else
             "机器检查已完成；未提供产品负责人批准的视觉基准时，不自动宣称视觉匹配。")
}
serialized = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
(EVIDENCE / "visual-qa-report.json").write_text(serialized, encoding="utf-8")
(EVIDENCE / f"visual-qa-report-{MODE}.json").write_text(serialized, encoding="utf-8")
print(json.dumps(report, ensure_ascii=False))
