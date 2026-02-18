#!/usr/bin/env python3
"""
CV-only segmentation baseline for Unity (archived comparison script).

CLI:
  python cv_segmentation_for_unity.py --in "/path/to/captured.png" --out "/path/to/run_dir"
  python cv_segmentation_for_unity.py --in test_images/captured.png --out test_run

Outputs (in OUT_DIR):
  - objects_mask.png            (binary mask: 255=object)
  - objects_only_rgba.png       (objects with transparent background)
  - objects_contour.json        (outer polygons in pixel coords)
  - overlay.png                 (objects highlighted over original)
  - debug_cv.png                (intermediate debug montage)
  - bg_color.png                (estimated background color patch)
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import List

import cv2
import numpy as np
from PIL import Image

# -------- Contour export tunables --------
MIN_CONTOUR_AREA = 800     # px^2 (lower if small objects are being dropped)
EPS_FRACTION = 0.01        # polygon simplification
MAX_POINTS = 256

# -------- CV tunables --------
BORDER = 12                # px: sample background color from border
BG_DIST_THRESH = 28        # higher => fewer pixels considered "object"
OPEN_ITERS = 1
CLOSE_ITERS = 2


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser()
    p.add_argument("--in", dest="in_path", type=Path, required=True)
    p.add_argument("--out", dest="out_dir", type=Path, required=True)
    return p.parse_args()


def overlay_mask(rgb: np.ndarray, mask01: np.ndarray, alpha: float = 0.55) -> np.ndarray:
    out = rgb.astype(np.float32).copy()
    m = mask01.astype(bool)
    color = np.array([0, 255, 0], dtype=np.float32)
    out[m] = out[m] * (1 - alpha) + color * alpha
    return np.clip(out, 0, 255).astype(np.uint8)


def save_mask_png(mask01: np.ndarray, path: Path):
    Image.fromarray((mask01.astype(np.uint8) * 255)).save(path)


def _simplify_polygon(poly_xy: np.ndarray) -> List[List[int]]:
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


def mask_to_contours_json(mask01: np.ndarray, out_json: Path, image_w: int, image_h: int) -> int:
    m = (mask01.astype(np.uint8) * 255)

    # Mild cleanup for contour stability
    kernel = np.ones((3, 3), np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, kernel, iterations=1)
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, kernel, iterations=1)

    contours, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    polys = []
    for c in contours:
        area = cv2.contourArea(c)
        if area < MIN_CONTOUR_AREA:
            continue

        c_xy = c.reshape(-1, 2)
        poly = _simplify_polygon(c_xy)
        if len(poly) >= 3:
            polys.append(
                {
                    "label": "object",
                    "outer": poly,
                    "holes": [],
                    "area_px": float(area),
                }
            )

    polys.sort(key=lambda p: p.get("area_px", 0), reverse=True)

    payload = {
        "image_w": int(image_w),
        "image_h": int(image_h),
        "polygons": polys,
        "notes": "CV segmentation. Coordinates are image pixels (origin top-left). Unity converts to local collider points.",
    }
    out_json.write_text(json.dumps(payload, indent=2))
    return len(polys)


def estimate_bg_color(rgb: np.ndarray, border: int) -> np.ndarray:
    h, w = rgb.shape[:2]
    b = max(1, min(border, min(h, w) // 4))

    top = rgb[:b, :, :]
    bottom = rgb[h - b :, :, :]
    left = rgb[:, :b, :]
    right = rgb[:, w - b :, :]

    samples = np.concatenate(
        [top.reshape(-1, 3), bottom.reshape(-1, 3), left.reshape(-1, 3), right.reshape(-1, 3)],
        axis=0,
    )

    # robust: median color
    return np.median(samples, axis=0).astype(np.uint8)


def cv_segment_objects(rgb: np.ndarray) -> np.ndarray:
    """
    Returns mask01 (H,W) where 1=object, 0=background.
    Strategy:
      - estimate background color from border
      - compute per-pixel color distance to background
      - threshold to get foreground
      - morph cleanup
    """
    bg = estimate_bg_color(rgb, BORDER).astype(np.int16)
    diff = rgb.astype(np.int16) - bg[None, None, :]
    dist = np.sqrt((diff * diff).sum(axis=2)).astype(np.float32)

    # foreground = pixels far enough from bg
    mask = (dist > float(BG_DIST_THRESH)).astype(np.uint8)

    kernel = np.ones((3, 3), np.uint8)
    if OPEN_ITERS > 0:
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel, iterations=OPEN_ITERS)
    if CLOSE_ITERS > 0:
        mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel, iterations=CLOSE_ITERS)

    return mask


def main():
    args = parse_args()
    in_path: Path = args.in_path
    out_dir: Path = args.out_dir
    out_dir.mkdir(parents=True, exist_ok=True)

    bgr = cv2.imread(str(in_path))
    if bgr is None:
        raise RuntimeError(f"Could not read image: {in_path.resolve()}")

    rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    h, w = rgb.shape[:2]

    print(f"[INFO] in:  {in_path.resolve()}")
    print(f"[INFO] out: {out_dir.resolve()}")
    print(f"[INFO] image: {w}x{h}")

    mask01 = cv_segment_objects(rgb)

    # Save masks + visuals
    save_mask_png(mask01, out_dir / "objects_mask.png")

    rgba_obj = np.dstack([rgb, (mask01 * 255).astype(np.uint8)])
    Image.fromarray(rgba_obj).save(out_dir / "objects_only_rgba.png")

    overlay = overlay_mask(rgb, mask01)
    Image.fromarray(overlay).save(out_dir / "overlay.png")

    # Debug montage
    bg = estimate_bg_color(rgb, BORDER)
    bg_patch = np.full((80, 80, 3), bg, dtype=np.uint8)
    dist_vis = None
    try:
        bg_i16 = bg.astype(np.int16)
        diff = rgb.astype(np.int16) - bg_i16[None, None, :]
        dist = np.sqrt((diff * diff).sum(axis=2)).astype(np.float32)
        dist_norm = cv2.normalize(dist, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)
        dist_vis = cv2.cvtColor(dist_norm, cv2.COLOR_GRAY2RGB)
    except Exception:
        dist_vis = np.zeros_like(rgb)

    mask_vis = (mask01 * 255).astype(np.uint8)
    mask_vis = cv2.cvtColor(mask_vis, cv2.COLOR_GRAY2RGB)
    debug = np.concatenate(
        [
            cv2.resize(rgb, (w // 2, h // 2)),
            cv2.resize(dist_vis, (w // 2, h // 2)),
            cv2.resize(mask_vis, (w // 2, h // 2)),
            cv2.resize(overlay, (w // 2, h // 2)),
        ],
        axis=1,
    )
    Image.fromarray(debug).save(out_dir / "debug_cv.png")
    Image.fromarray(bg_patch).save(out_dir / "bg_color.png")

    n_polys = mask_to_contours_json(mask01, out_dir / "objects_contour.json", w, h)

    # Also print connected component count for quick sanity
    num_labels, _ = cv2.connectedComponents((mask01 * 255).astype(np.uint8))
    comps = num_labels - 1

    print(f"[INFO] connected components: {comps}")
    print(f"[INFO] exported polygons: {n_polys}")

    print(f"[SAVED] {out_dir / 'objects_mask.png'}")
    print(f"[SAVED] {out_dir / 'objects_only_rgba.png'}")
    print(f"[SAVED] {out_dir / 'overlay.png'}")
    print(f"[SAVED] {out_dir / 'debug_cv.png'}")
    print(f"[SAVED] {out_dir / 'bg_color.png'}")
    print(f"[SAVED] {out_dir / 'objects_contour.json'}")

    if n_polys == 0:
        print("[WARN] 0 polygons. Lower MIN_CONTOUR_AREA or lower BG_DIST_THRESH.")
    if comps < 2:
        print("[WARN] Very few components. Lower BG_DIST_THRESH or improve lighting/contrast.")
    if comps > 50:
        print("[WARN] Too many components (noise). Increase BG_DIST_THRESH and/or increase OPEN_ITERS.")


if __name__ == "__main__":
    main()
