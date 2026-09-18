#!/usr/bin/env python3
"""Cut the continuous real-play WebM into silent H.264 MP4 validation clips."""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
from typing import Any

import cv2
from PIL import Image, ImageDraw, ImageFont


VALIDATION_ROOT = Path(__file__).resolve().parent
RAW_VIDEO = VALIDATION_ROOT / "raw" / "phase-ripple-continuous.webm"
CAPTURE_EVIDENCE = VALIDATION_ROOT / "evidence" / "capture-run.json"
MEDIA_REPORT = VALIDATION_ROOT / "evidence" / "media-report.json"
VIDEO_ROOT = VALIDATION_ROOT / "videos"
COVER_ROOT = VALIDATION_ROOT / "cover"
CONTACT_COVER = COVER_ROOT / "phase-ripple-contact-cover.png"
WIDTH = 390
HEIGHT = 844
CODEC = "avc1"
OUTPUT_DURATION_SECONDS = 6.4
FONT_PATH = Path("/System/Library/Fonts/Hiragino Sans GB.ttc")


def decode_fourcc(value: int) -> str:
    return "".join(chr((value >> (8 * index)) & 0xFF) for index in range(4))


def segment_frame_bounds(start_seconds: float, duration_seconds: float, fps: float) -> tuple[int, int]:
    if not math.isfinite(fps) or fps <= 0:
        raise ValueError(f"Invalid frame rate: {fps}")
    return round(start_seconds * fps), round(duration_seconds * fps)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def probe_video(path: Path) -> dict[str, Any]:
    capture = cv2.VideoCapture(str(path))
    if not capture.isOpened():
        raise RuntimeError(f"Cannot open video: {path}")
    fps = float(capture.get(cv2.CAP_PROP_FPS))
    frames = int(round(capture.get(cv2.CAP_PROP_FRAME_COUNT)))
    fourcc = decode_fourcc(int(capture.get(cv2.CAP_PROP_FOURCC)))
    result = {
        "path": str(path),
        "width": int(round(capture.get(cv2.CAP_PROP_FRAME_WIDTH))),
        "height": int(round(capture.get(cv2.CAP_PROP_FRAME_HEIGHT))),
        "fps": fps,
        "frames": frames,
        "durationSeconds": frames / fps if fps > 0 else 0,
        "codec": fourcc,
        "sha256": sha256_file(path),
    }
    capture.release()
    return result


def encode_segment(
    raw_capture: cv2.VideoCapture,
    output_path: Path,
    start_frame: int,
    frame_count: int,
    fps: float,
) -> None:
    if output_path.exists():
        output_path.unlink()
    writer = cv2.VideoWriter(
        str(output_path),
        cv2.VideoWriter_fourcc(*CODEC),
        fps,
        (WIDTH, HEIGHT),
    )
    if not writer.isOpened():
        raise RuntimeError(f"Cannot open {CODEC} writer for {output_path}")
    raw_capture.set(cv2.CAP_PROP_POS_FRAMES, start_frame)
    written = 0
    try:
        while written < frame_count:
            ok, frame = raw_capture.read()
            if not ok:
                raise RuntimeError(
                    f"Raw video ended after {written}/{frame_count} frames for {output_path.name}"
                )
            if frame.shape[1] != WIDTH or frame.shape[0] != HEIGHT:
                raise RuntimeError(f"Unexpected raw frame size: {frame.shape[1]}x{frame.shape[0]}")
            writer.write(frame)
            written += 1
    finally:
        writer.release()


def create_contact_cover(video_path: Path, output_path: Path) -> None:
    capture = cv2.VideoCapture(str(video_path))
    if not capture.isOpened():
        raise RuntimeError(f"Cannot open cover source: {video_path}")
    capture.set(cv2.CAP_PROP_POS_MSEC, 1800)
    ok, frame = capture.read()
    capture.release()
    if not ok:
        raise RuntimeError(f"Cannot read cover frame: {video_path}")

    image = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)).convert("RGBA")
    overlay = Image.new("RGBA", image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    draw.rounded_rectangle((16, 14, WIDTH - 16, 112), radius=8, fill=(3, 9, 11, 235), outline=(108, 245, 221, 255), width=2)
    title_font = ImageFont.truetype(str(FONT_PATH), 34)
    detail_font = ImageFont.truetype(str(FONT_PATH), 20)
    draw.text((WIDTH / 2, 29), "一次涟漪", font=title_font, fill=(234, 248, 245, 255), anchor="ma")
    draw.text(
        (WIDTH / 2, 78),
        "让 6 颗同时回到中心",
        font=detail_font,
        fill=(108, 245, 221, 255),
        anchor="ma",
    )
    Image.alpha_composite(image, overlay).convert("RGB").save(output_path, quality=95)


def main() -> None:
    if not RAW_VIDEO.exists() or not CAPTURE_EVIDENCE.exists():
        raise SystemExit("Run capture.mjs before encode.py")
    VIDEO_ROOT.mkdir(parents=True, exist_ok=True)
    COVER_ROOT.mkdir(parents=True, exist_ok=True)

    evidence = json.loads(CAPTURE_EVIDENCE.read_text(encoding="utf-8"))
    source_probe = probe_video(RAW_VIDEO)
    if (source_probe["width"], source_probe["height"]) != (WIDTH, HEIGHT):
        raise RuntimeError(f"Raw video is not {WIDTH}x{HEIGHT}: {source_probe}")

    raw_capture = cv2.VideoCapture(str(RAW_VIDEO))
    if not raw_capture.isOpened():
        raise RuntimeError(f"Cannot open raw video: {RAW_VIDEO}")
    clips = []
    try:
        for capture in evidence["captures"]:
            start_frame, frame_count = segment_frame_bounds(
                float(capture["rawStartSeconds"]),
                OUTPUT_DURATION_SECONDS,
                float(source_probe["fps"]),
            )
            output_path = VIDEO_ROOT / f"{capture['id']}.mp4"
            encode_segment(raw_capture, output_path, start_frame, frame_count, source_probe["fps"])
            probe = probe_video(output_path)
            if (probe["width"], probe["height"]) != (WIDTH, HEIGHT):
                raise RuntimeError(f"Wrong output size: {probe}")
            if not 6 <= probe["durationSeconds"] <= 10:
                raise RuntimeError(f"Wrong output duration: {probe}")
            if probe["codec"].lower() not in {"avc1", "h264"}:
                raise RuntimeError(f"Output is not H.264: {probe}")
            clips.append(
                {
                    "id": capture["id"],
                    "seed": capture["seed"],
                    "pieceCount": capture["pieceCount"],
                    "category": capture["category"],
                    "hook": capture["hook"],
                    "expectedOutcome": capture["expectedOutcome"],
                    "actualOutcome": capture["actualOutcome"],
                    "rawStartSeconds": capture["rawStartSeconds"],
                    "startFrame": start_frame,
                    "capturedHoldSeconds": capture["requestedDurationSeconds"],
                    "outputDurationTargetSeconds": OUTPUT_DURATION_SECONDS,
                    "audioStreams": 0,
                    "audioVerification": "OpenCV frame-only VideoWriter created no audio stream",
                    **probe,
                }
            )
            print(
                f"encoded {output_path.name}: {probe['codec']} "
                f"{probe['width']}x{probe['height']} {probe['durationSeconds']:.2f}s"
            )
    finally:
        raw_capture.release()

    cover_source = VIDEO_ROOT / "05-sync-6.mp4"
    create_contact_cover(cover_source, CONTACT_COVER)
    report = {
        "generatedAt": evidence["generatedAt"],
        "source": source_probe,
        "encoding": {
            "container": "MP4",
            "videoCodec": "H.264/avc1",
            "audioStreams": 0,
            "method": "OpenCV VideoWriter backed by its bundled FFmpeg libraries",
        },
        "clips": sorted(clips, key=lambda clip: clip["id"]),
        "cover": {
            "path": str(CONTACT_COVER),
            "width": WIDTH,
            "height": HEIGHT,
            "sourceClip": "05-sync-6.mp4",
            "sourceTimeSeconds": 1.8,
            "sha256": sha256_file(CONTACT_COVER),
        },
    }
    MEDIA_REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"cover {CONTACT_COVER}")
    print(f"report {MEDIA_REPORT}")


if __name__ == "__main__":
    main()
