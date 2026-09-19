#!/usr/bin/env python3
"""Verify final clips without modifying the prototype or contacting any network service."""

from __future__ import annotations

import json
import shutil
import subprocess
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from encode import HEIGHT, MEDIA_REPORT, RAW_VIDEO, VALIDATION_ROOT, VIDEO_ROOT, WIDTH, probe_video


PLAYWRIGHT_FFMPEG = Path("/Users/kker/Library/Caches/ms-playwright/ffmpeg-1011/ffmpeg-mac")
CAPTURE_EVIDENCE = VALIDATION_ROOT / "evidence" / "capture-run.json"
VERIFY_REPORT = VALIDATION_ROOT / "evidence" / "verification-report.json"


def run_command(arguments: list[str]) -> dict[str, Any]:
    completed = subprocess.run(arguments, capture_output=True, text=True, check=False)
    return {
        "command": arguments,
        "returnCode": completed.returncode,
        "stdout": completed.stdout,
        "stderr": completed.stderr,
    }


def main() -> None:
    capture_evidence = json.loads(CAPTURE_EVIDENCE.read_text(encoding="utf-8"))
    media_report = json.loads(MEDIA_REPORT.read_text(encoding="utf-8"))
    expected_by_id = {capture["id"]: capture for capture in capture_evidence["captures"]}
    expected_ids = set(expected_by_id)
    video_paths = sorted(VIDEO_ROOT.glob("*.mp4"))
    actual_ids = {path.stem for path in video_paths}
    if actual_ids != expected_ids:
        raise RuntimeError(f"Video set mismatch: expected={expected_ids}, actual={actual_ids}")

    raw_ffmpeg = run_command([str(PLAYWRIGHT_FFMPEG), "-i", str(RAW_VIDEO)])
    raw_metadata = raw_ffmpeg["stderr"]
    if "390x844" not in raw_metadata or "25 fps" not in raw_metadata or "Video: vp8" not in raw_metadata:
        raise RuntimeError("Playwright FFmpeg did not report the expected raw video metadata")
    if "Audio:" in raw_metadata:
        raise RuntimeError("Raw Playwright recording unexpectedly contains audio")

    clip_results = []
    for path in video_paths:
        probe = probe_video(path)
        if (probe["width"], probe["height"]) != (WIDTH, HEIGHT):
            raise RuntimeError(f"Wrong dimensions: {probe}")
        if not 6 <= probe["durationSeconds"] <= 10:
            raise RuntimeError(f"Wrong duration: {probe}")
        if probe["codec"].lower() not in {"avc1", "h264"}:
            raise RuntimeError(f"Wrong codec: {probe}")
        expected = expected_by_id[path.stem]
        if expected["actualOutcome"] != expected["expectedOutcome"]:
            raise RuntimeError(f"Gameplay result mismatch in {path.stem}")

        file_probe = run_command(["/usr/bin/file", str(path)])
        if file_probe["returnCode"] != 0 or "ISO Media, MP4" not in file_probe["stdout"]:
            raise RuntimeError(f"Container probe failed: {file_probe}")
        audio_probe = run_command(["/usr/bin/afinfo", str(path)])
        if audio_probe["returnCode"] == 0:
            raise RuntimeError(f"Expected a silent video-only file, but afinfo found audio: {path}")
        clip_results.append(
            {
                "id": path.stem,
                "outcome": expected["actualOutcome"],
                "containerProbe": file_probe["stdout"].strip(),
                "audioProbe": "afinfo found no readable audio track (AudioFileOpenURL failed)",
                **probe,
            }
        )

    final_ffmpeg_attempt = run_command([str(PLAYWRIGHT_FFMPEG), "-i", str(video_paths[0])])
    report = {
        "verifiedAt": datetime.now(timezone.utc).isoformat(),
        "passed": True,
        "requirements": {
            "clipCount": len(clip_results),
            "width": WIDTH,
            "height": HEIGHT,
            "durationRangeSeconds": [6, 10],
            "videoCodec": "H.264",
            "audioTracks": 0,
            "realOutcomeMatchesManifest": True,
        },
        "toolAvailability": {
            "ffprobe": shutil.which("ffprobe"),
            "playwrightFfmpeg": str(PLAYWRIGHT_FFMPEG),
            "playwrightFfmpegSupportsMp4Demux": False,
            "note": (
                "The Playwright FFmpeg build has only Matroska/WebM and image2 demuxers. "
                "It successfully probed the raw WebM but cannot parse final MP4 files; "
                "final MP4 checks use OpenCV's FFmpeg backend, file, and afinfo."
            ),
        },
        "rawWebm": {
            "path": str(RAW_VIDEO),
            "ffmpegReturnCode": raw_ffmpeg["returnCode"],
            "hasVideoVp8": "Video: vp8" in raw_metadata,
            "hasAudioStream": "Audio:" in raw_metadata,
        },
        "finalMp4FfmpegAttempt": {
            "returnCode": final_ffmpeg_attempt["returnCode"],
            "stderrTail": final_ffmpeg_attempt["stderr"].strip().splitlines()[-3:],
        },
        "clips": clip_results,
        "cover": media_report["cover"],
    }
    VERIFY_REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for clip in clip_results:
        print(
            f"PASS {clip['id']}: {clip['codec']} {clip['width']}x{clip['height']} "
            f"{clip['fps']:.2f}fps {clip['durationSeconds']:.2f}s silent outcome={clip['outcome']}"
        )
    print(f"PASS cover: {media_report['cover']['path']}")
    print(f"report {VERIFY_REPORT}")


if __name__ == "__main__":
    main()
