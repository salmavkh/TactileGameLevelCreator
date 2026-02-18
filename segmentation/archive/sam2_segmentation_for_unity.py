#!/usr/bin/env python3
"""
SAM2 segmentation pipeline for Unity (archived comparison script).

CLI:
  python sam2_segmentation_for_unity.py --in "/path/to/captured.png" --out "/path/to/run_dir"

Defaults (same behavior as before):
  --in  ./out/captured.png
  --out ./out

Outputs (in OUT_DIR):
  - overlay.png                 (mask overlay visualization)
  - segmented_rgba.png          (union of all masks as alpha)
  - objects_only_rgba.png       (foreground-only RGBA after background removal)
  - background_mask.png         (largest mask treated as background)
  - objects_mask.png            (union of non-background masks)
  - objects_contour.json        (Unity collider polygon(s) in image pixel coords)
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import List

import cv2
import numpy as np
from PIL import Image
import torch

from sam2.build_sam import build_sam2
from sam2.automatic_mask_generator import SAM2AutomaticMaskGenerator


# ---- Defaults (keep same as your current script) ----
DEFAULT_IN_PATH = Path("./out/captured.png")
DEFAULT_OUT_DIR = Path("./out")

# ---- SAM2 checkpoint/config (tiny is best to start on M1) ----
CHECKPOINT_PATH = Path("../sam2/checkpoints/sam2.1_hiera_tiny.pt")
MODEL_CFG = "configs/sam2.1/sam2.1_hiera_t.yaml"

# ---- Contour export tunables ----
MIN_CONTOUR_AREA = 800   # Filter tiny blobs from collider export (in pixels)
EPS_FRACTION = 0.01      # approx_epsilon = EPS_FRACTION * perimeter
MAX_POINTS = 256         # Limit points per polygon for physics stability


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Process one image with SAM2 and export Unity-ready outputs.")
    p.add_argument("--in", dest="in_path", type=Path, default=DEFAULT_IN_PATH, help="Input image path (png/jpg).")
    p.add_argument("--out", dest="out_dir", type=Path, default=DEFAULT_OUT_DIR, help="Output directory.")
    return p.parse_args()


def get_device() -> torch.device:
    # Note: torch MPS requires macOS 14+. On macOS 13, this will likely be unavailable.
    if torch.backends.mps.is_available():
        return torch.device("mps")
    return torch.device("cpu")


def overlay_masks(rgb: np.ndarray, masks01: np.ndarray, alpha: float = 0.55) -> np.ndarray:
    out = rgb.astype(np.float32).copy()
    palette = np.array(
        [
            (255, 0, 0),
            (0, 255, 0),
            (0, 0, 255),
            (255, 255, 0),
            (255, 0, 255),
            (0, 255, 255),
            (255, 128, 0),
            (128, 0, 255),
        ],
        dtype=np.float32,
    )
    for i in range(masks01.shape[0]):
        m = masks01[i].astype(bool)
        color = palette[i % len(palette)]
        out[m] = out[m] * (1 - alpha) + color * alpha
    return np.clip(out, 0, 255).astype(np.uint8)


def save_mask_png(mask01: np.ndarray, path: Path):
    m = (mask01.astype(np.uint8) * 255)
    Image.fromarray(m).save(path)


def _simplify_polygon(poly_xy: np.ndarray) -> List[List[int]]:
    """
    poly_xy: (N,2) float/int in image pixel coords.
    Returns list of [x,y] ints, simplified and clamped.
    """
    if poly_xy.shape[0] < 3:
        return []

    pts = poly_xy.astype(np.float32).reshape(-1, 1, 2)
    peri = cv2.arcLength(pts, True)
    eps = max(1.0, EPS_FRACTION * peri)
    approx = cv2.approxPolyDP(pts, eps, True)  # (M,1,2)
    approx_xy = approx.reshape(-1, 2)

    if approx_xy.shape[0] > MAX_POINTS:
        idx = np.linspace(0, approx_xy.shape[0] - 1, MAX_POINTS).astype(int)
        approx_xy = approx_xy[idx]

    out = [[int(round(x)), int(round(y))] for x, y in approx_xy]
    if len(out) >= 2 and out[0] == out[-1]:
        out = out[:-1]
    return out


def mask_to_contours_json(mask01: np.ndarray, out_json: Path, image_w: int, image_h: int):
    """
    mask01: HxW uint8/0-1 mask of objects (1 = object)
    Exports one or more outer polygons for Unity PolygonCollider2D.
    Coordinates in image pixel space, origin top-left.
    """
    m = (mask01.astype(np.uint8) * 255)

    kernel = np.ones((3, 3), np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, kernel, iterations=1)
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, kernel, iterations=1)

    contours, _hier = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

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
        "notes": "Coordinates are in image pixels (origin top-left). Unity should convert to local collider points using pixelsPerUnit and flip Y.",
    }

    out_json.write_text(json.dumps(payload, indent=2))
    return len(polys)


def main():
    args = parse_args()
    in_path: Path = args.in_path
    out_dir: Path = args.out_dir

    if not in_path.exists():
        raise FileNotFoundError(f"Missing input image: {in_path.resolve()}")

    if not CHECKPOINT_PATH.exists():
        raise FileNotFoundError(
            f"Missing SAM2 checkpoint: {CHECKPOINT_PATH.resolve()}\n"
            f"Download it via the sam2 repo checkpoints/download_ckpts.sh"
        )

    out_dir.mkdir(parents=True, exist_ok=True)

    device = get_device()
    print(f"[INFO] device: {device}")
    print(f"[INFO] in:  {in_path.resolve()}")
    print(f"[INFO] out: {out_dir.resolve()}")

    bgr = cv2.imread(str(in_path))
    if bgr is None:
        raise RuntimeError(f"Could not read image: {in_path.resolve()}")

    rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    h, w = rgb.shape[:2]

    model = build_sam2(MODEL_CFG, str(CHECKPOINT_PATH), device=device, apply_postprocessing=False)
    mask_generator = SAM2AutomaticMaskGenerator(
        model,
        points_per_side=16,
        pred_iou_thresh=0.85,
        stability_score_thresh=0.90,
        crop_n_layers=0,
        crop_n_points_downscale_factor=2,
        min_mask_region_area=200,
    )

    with torch.inference_mode():
        masks = mask_generator.generate(rgb)

    print(f"[INFO] masks found: {len(masks)}")
    if len(masks) == 0:
        Image.fromarray(rgb).save(out_dir / "overlay.png")
        print("[WARN] No masks found. Saved overlay.png as the raw image.")
        return

    masks_sorted = sorted(masks, key=lambda m: m.get("area", 0), reverse=True)
    masks01 = np.stack([(m["segmentation"].astype(np.uint8)) for m in masks_sorted], axis=0)

    bg = masks01[0]
    objs = masks01[1:] if masks01.shape[0] > 1 else masks01[0:0]

    obj_union = np.zeros_like(bg, dtype=np.uint8)
    for m in objs:
        obj_union = np.maximum(obj_union, m)

    overlay_all = overlay_masks(rgb, masks01)
    Image.fromarray(overlay_all).save(out_dir / "overlay.png")

    alpha_all = (masks01.max(axis=0) * 255).astype(np.uint8)
    rgba_all = np.dstack([rgb, alpha_all])
    Image.fromarray(rgba_all).save(out_dir / "segmented_rgba.png")

    alpha_obj = (obj_union * 255).astype(np.uint8)
    rgba_obj = np.dstack([rgb, alpha_obj])
    Image.fromarray(rgba_obj).save(out_dir / "objects_only_rgba.png")

    save_mask_png(bg, out_dir / "background_mask.png")
    save_mask_png(obj_union, out_dir / "objects_mask.png")

    n_polys = mask_to_contours_json(
        mask01=obj_union,
        out_json=out_dir / "objects_contour.json",
        image_w=w,
        image_h=h,
    )

    print(f"[SAVED] {out_dir / 'overlay.png'}")
    print(f"[SAVED] {out_dir / 'segmented_rgba.png'}")
    print(f"[SAVED] {out_dir / 'objects_only_rgba.png'}")
    print(f"[SAVED] {out_dir / 'background_mask.png'}")
    print(f"[SAVED] {out_dir / 'objects_mask.png'}")
    print(f"[SAVED] {out_dir / 'objects_contour.json'}")
    print(f"[INFO] exported polygons: {n_polys}")
    if n_polys == 0:
        print("[WARN] No valid polygons exported (mask too small/noisy). Try lowering MIN_CONTOUR_AREA or adjusting SAM2 thresholds.")


if __name__ == "__main__":
    main()
