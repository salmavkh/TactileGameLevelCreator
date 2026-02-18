#!/usr/bin/env python3
"""
FastSAM -> Unity outputs (no SAM2).

pip install ultralytics opencv-python pillow numpy

CLI:
  python process_img_for_unity.py --in "/path/captured.png" --out "/path/run_dir" --weights "/path/FastSAM-s.pt"

python process_img_for_unity.py \
  --in test_images/captured.png \
  --out test_run 

Change FASTSAM_WEIGHTS below to swap models.

Writes (in OUT_DIR):
  - objects_mask.png            (union mask, 255=object)
  - objects_only_rgba.png       (objects with transparent background)
  - overlay.png                 (mask overlay on original)
  - debug_masks.png             (shows kept masks outlines + ids)
  - objects_contour.json        (polygons in pixel coords)
"""

from __future__ import annotations
import argparse
import json
from pathlib import Path
from typing import List

import cv2
import numpy as np
from PIL import Image
from ultralytics import FastSAM

# ============================
# === USER SETTINGS (edit) ===
# ============================

# Path to FastSAM model
FASTSAM_WEIGHTS = Path("./models/FastSAM-x.pt")   # change to FastSAM-x.pt or FastSAM-s.pt

# Inference quality/speed
IMGSZ = 768        # 512 faster, 1024 more accurate
CONF  = 0.4
IOU   = 0.9

# If the largest mask covers more than this fraction of the image, drop it as "background"
DROP_LARGEST_BG_FRAC = 0.55

# Contour export
MIN_CONTOUR_AREA = 800
EPS_FRACTION = 0.01
MAX_POINTS = 256

# ============================


def parse_args():
    p = argparse.ArgumentParser()
    p.add_argument("--in", dest="in_path", type=Path, required=True)
    p.add_argument("--out", dest="out_dir", type=Path, required=True)
    return p.parse_args()


def overlay_mask(rgb, mask01, alpha=0.55):
    out = rgb.astype(np.float32).copy()
    m = mask01.astype(bool)
    color = np.array([0, 255, 0], dtype=np.float32)
    out[m] = out[m] * (1 - alpha) + color * alpha
    return np.clip(out, 0, 255).astype(np.uint8)


def save_mask_png(mask01, path):
    Image.fromarray((mask01.astype(np.uint8) * 255)).save(path)


def simplify_polygon(poly_xy):
    if poly_xy.shape[0] < 3:
        return []

    pts = poly_xy.astype(np.float32).reshape(-1, 1, 2)
    peri = cv2.arcLength(pts, True)
    eps = max(1.0, EPS_FRACTION * peri)
    approx = cv2.approxPolyDP(pts, eps, True)
    approx_xy = approx.reshape(-1, 2)

    if approx_xy.shape[0] > MAX_POINTS:
        idx = np.linspace(0, approx_xy.shape[0] - 1, MAX_POINTS).astype(int)
        approx_xy = approx_xy[idx]

    out = [[int(round(x)), int(round(y))] for x, y in approx_xy]
    if len(out) >= 2 and out[0] == out[-1]:
        out = out[:-1]
    return out


def export_contours(mask01, out_json, w, h):
    m = (mask01.astype(np.uint8) * 255)

    kernel = np.ones((3, 3), np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, kernel, iterations=1)
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, kernel, iterations=1)

    contours, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    polys = []
    for c in contours:
        area = cv2.contourArea(c)
        if area < MIN_CONTOUR_AREA:
            continue

        poly = simplify_polygon(c.reshape(-1, 2))
        if len(poly) >= 3:
            polys.append({
                "label": "object",
                "outer": poly,
                "holes": [],
                "area_px": float(area)
            })

    polys.sort(key=lambda p: p["area_px"], reverse=True)

    payload = {
        "image_w": int(w),
        "image_h": int(h),
        "polygons": polys,
        "notes": "FastSAM segmentation. Pixel coords (origin top-left)."
    }

    out_json.write_text(json.dumps(payload, indent=2))
    return len(polys)


def get_masks_from_result(res0):
    data = res0.masks.data
    arr = data.detach().cpu().numpy()
    return (arr > 0.5).astype(np.uint8)


def main():
    args = parse_args()
    in_path = args.in_path
    out_dir = args.out_dir
    out_dir.mkdir(parents=True, exist_ok=True)

    if not FASTSAM_WEIGHTS.exists():
        raise FileNotFoundError(f"FastSAM weights not found: {FASTSAM_WEIGHTS.resolve()}")

    bgr = cv2.imread(str(in_path))
    if bgr is None:
        raise RuntimeError("Failed to load input image")

    rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    h, w = rgb.shape[:2]
    img_area = float(h * w)

    print("[INFO] FastSAM weights:", FASTSAM_WEIGHTS.resolve())
    print("[INFO] Image:", w, "x", h)

    model = FastSAM(str(FASTSAM_WEIGHTS))
    results = model(str(in_path), retina_masks=True, imgsz=IMGSZ, conf=CONF, iou=IOU)
    res0 = results[0]

    if res0.masks is None:
        print("[WARN] No masks detected")
        Image.fromarray(rgb).save(out_dir / "overlay.png")
        return

    masks = get_masks_from_result(res0)
    areas = masks.reshape(masks.shape[0], -1).sum(axis=1)
    order = np.argsort(-areas)
    masks = masks[order]
    areas = areas[order]

    if areas[0] / img_area >= DROP_LARGEST_BG_FRAC and masks.shape[0] > 1:
        print("[INFO] Dropping largest mask as background")
        masks = masks[1:]

    union = np.zeros((h, w), dtype=np.uint8)
    for m in masks:
        union = np.maximum(union, m)

    save_mask_png(union, out_dir / "objects_mask.png")

    rgba = np.dstack([rgb, (union * 255).astype(np.uint8)])
    Image.fromarray(rgba).save(out_dir / "objects_only_rgba.png")

    overlay = overlay_mask(rgb, union)
    Image.fromarray(overlay).save(out_dir / "overlay.png")

    n = export_contours(union, out_dir / "objects_contour.json", w, h)

    print("[INFO] Objects:", n)
    print("[SAVED] objects_only_rgba.png, objects_mask.png, overlay.png, objects_contour.json")


if __name__ == "__main__":
    main()
