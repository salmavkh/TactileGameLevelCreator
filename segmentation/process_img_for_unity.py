#!/usr/bin/env python3
"""
FastSAM -> Unity outputs (no SAM2).

pip install ultralytics opencv-python pillow numpy

CLI:
  python process_img_for_unity.py --in "/path/captured.png" --out "/path/run_dir"

Writes (in OUT_DIR):
  - objects_mask.png            (union mask of kept masks, 255=object)
  - objects_only_rgba.png       (objects with transparent background)
  - overlay.png                 (mask overlay on original)
  - objects_contour.json        (polygons in pixel coords; exported per kept mask)
"""

from __future__ import annotations
import argparse
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image
from ultralytics import FastSAM

# ============================
# === USER SETTINGS (edit) ===
# ============================

FASTSAM_WEIGHTS = Path("./models/FastSAM-x.pt")

IMGSZ = 768
CONF  = 0.4
IOU   = 0.9

# ---- Background heuristics (practical) ----
# 1) Too big => likely background blob (even if not touching borders)
BG_AREA_FRAC_TH = 0.45        # try 0.35–0.60

# 2) Touches borders a lot
BORDER_TOUCH_FRAC_TH = 0.10   # try 0.05–0.20
BORDER_BAND_PX = 3            # border band thickness used to count border pixels

# 3) Holes / weird shape (optional)
ENABLE_HOLE_HEURISTIC = True
HOLE_FRAC_TH = 0.08           # holes area / mask area
MIN_HOLE_AREA_PX = 80         # ignore tiny pinholes

# ---- Noise floor ----
MIN_INSTANCE_AREA_PX = 150

# ---- Contour export ----
MIN_CONTOUR_AREA = 200
EPS_FRACTION = 0.01
MAX_POINTS = 256

# ---- Morphology (avoid merging objects) ----
DO_OPEN = True
DO_CLOSE = False              # CLOSE can MERGE nearby objects into background-like blobs
MORPH_ITERS = 1

# ---- Debug ----
DEBUG_PRINT_MASK_STATS = True
DEBUG_SAVE_KEEP_DROP_IMAGES = True   # writes debug_keep_drop.png

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


def simplify_polygon(poly_xy: np.ndarray):
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


def _morph_clean(binary255: np.ndarray) -> np.ndarray:
    m = binary255
    kernel = np.ones((3, 3), np.uint8)
    if DO_OPEN:
        m = cv2.morphologyEx(m, cv2.MORPH_OPEN, kernel, iterations=MORPH_ITERS)
    if DO_CLOSE:
        m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, kernel, iterations=MORPH_ITERS)
    return m


def border_touch_frac(mask01: np.ndarray, band_px: int = 3) -> float:
    """Fraction of mask pixels that lie within a border band."""
    m = mask01.astype(bool)
    total = float(m.sum())
    if total <= 1e-9:
        return 0.0

    h, w = m.shape
    b = max(1, int(band_px))

    border = np.zeros((h, w), dtype=bool)
    border[:b, :] = True
    border[-b:, :] = True
    border[:, :b] = True
    border[:, -b:] = True

    touch = float((m & border).sum())
    return touch / total


def hole_frac(mask01: np.ndarray, min_hole_area_px: int = 80) -> float:
    """
    Approx hole area / mask area.
    Fill holes by flood fill on inverted mask. Remaining enclosed regions are holes.
    """
    m = (mask01.astype(np.uint8) * 255)
    total = float((m > 0).sum())
    if total <= 1e-9:
        return 0.0

    inv = cv2.bitwise_not(m)
    h, w = inv.shape[:2]
    ff = inv.copy()
    flood_mask = np.zeros((h + 2, w + 2), np.uint8)
    cv2.floodFill(ff, flood_mask, (0, 0), 0)

    holes = (ff > 0).astype(np.uint8)

    # remove tiny holes
    num, lbl, stats, _ = cv2.connectedComponentsWithStats(holes, connectivity=8)
    holes_big = np.zeros_like(holes)
    for i in range(1, num):
        area = stats[i, cv2.CC_STAT_AREA]
        if area >= min_hole_area_px:
            holes_big[lbl == i] = 1

    hole_area = float(holes_big.sum())
    return hole_area / total


def is_background_like(mask01: np.ndarray) -> tuple[bool, str, dict]:
    area = int(mask01.sum())
    h, w = mask01.shape
    area_frac = area / float(h * w) if (h * w) > 0 else 0.0
    bfrac = border_touch_frac(mask01, BORDER_BAND_PX)
    hfrac = hole_frac(mask01, MIN_HOLE_AREA_PX) if ENABLE_HOLE_HEURISTIC else 0.0

    stats = {
        "area": area,
        "area_frac": area_frac,
        "border_touch_frac": bfrac,
        "hole_frac": hfrac,
    }

    if area < MIN_INSTANCE_AREA_PX:
        return True, f"too_small area={area}", stats

    # Practical background catch: very large interior blob
    if area_frac >= BG_AREA_FRAC_TH:
        return True, f"too_large area_frac={area_frac:.3f} >= {BG_AREA_FRAC_TH}", stats

    # Typical background: touches borders a lot
    if bfrac >= BORDER_TOUCH_FRAC_TH:
        return True, f"border_touch_frac={bfrac:.3f} >= {BORDER_TOUCH_FRAC_TH}", stats

    # Optional weird/holed background
    if ENABLE_HOLE_HEURISTIC and hfrac >= HOLE_FRAC_TH:
        return True, f"hole_frac={hfrac:.3f} >= {HOLE_FRAC_TH}", stats

    return False, "kept", stats


def export_contours_from_masks(masks01: np.ndarray, out_json: Path, w: int, h: int) -> int:
    polys = []
    kept = 0

    for mi in range(masks01.shape[0]):
        m01 = masks01[mi].astype(np.uint8)
        m255 = _morph_clean(m01 * 255)

        contours, _ = cv2.findContours(m255, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        if not contours:
            print(f"[DBG] mask {mi}: 0 contours")
            continue

        contours = sorted(contours, key=cv2.contourArea, reverse=True)
        c = contours[0]
        area = cv2.contourArea(c)

        if area < MIN_CONTOUR_AREA:
            print(f"[DBG] mask {mi}: contour too small area={area:.1f} < MIN_CONTOUR_AREA={MIN_CONTOUR_AREA}")
            continue

        poly = simplify_polygon(c.reshape(-1, 2))
        if len(poly) < 3:
            print(f"[DBG] mask {mi}: simplify produced <3 points")
            continue

        polys.append({
            "label": "object",
            "outer": poly,
            "holes": [],
            "area_px": float(area),
            "mask_index": int(mi),
        })
        kept += 1
        print(f"[DBG] mask {mi}: polygon kept area={area:.1f}, points={len(poly)}")

    polys.sort(key=lambda p: p["area_px"], reverse=True)

    payload = {
        "image_w": int(w),
        "image_h": int(h),
        "polygons": polys,
        "notes": "FastSAM per-instance masks. Background excluded via area + border-touch + hole heuristics."
    }
    out_json.write_text(json.dumps(payload, indent=2))
    return kept


def get_masks_from_result(res0) -> np.ndarray:
    arr = res0.masks.data.detach().cpu().numpy()
    return (arr > 0.5).astype(np.uint8)


def make_debug_keep_drop_image(rgb: np.ndarray, keep_union: np.ndarray, drop_union: np.ndarray) -> np.ndarray:
    """
    Visual debug image:
      - kept masks overlayed in GREEN
      - dropped masks overlayed in RED
    """
    out = rgb.astype(np.float32).copy()

    keep = keep_union.astype(bool)
    drop = drop_union.astype(bool)

    green = np.array([0, 255, 0], dtype=np.float32)
    red = np.array([255, 0, 0], dtype=np.float32)

    alpha = 0.55
    out[keep] = out[keep] * (1 - alpha) + green * alpha
    out[drop] = out[drop] * (1 - alpha) + red * alpha

    return np.clip(out, 0, 255).astype(np.uint8)


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
    print("[INFO] Input image:", w, "x", h)
    if w <= 256 or h <= 256:
        print("[WARN] Image is very small. Objects may merge/disappear. Use higher-res capture if possible.")

    model = FastSAM(str(FASTSAM_WEIGHTS))
    results = model(str(in_path), retina_masks=True, imgsz=IMGSZ, conf=CONF, iou=IOU)
    res0 = results[0]

    if res0.masks is None:
        print("[WARN] No masks detected")
        Image.fromarray(rgb).save(out_dir / "overlay.png")
        return

    masks = get_masks_from_result(res0)  # (N,H,W)
    areas = masks.reshape(masks.shape[0], -1).sum(axis=1)
    order = np.argsort(-areas)
    masks = masks[order]
    areas = areas[order]

    print(f"[INFO] Raw masks: {masks.shape[0]}")
    if masks.shape[0] > 0:
        print(f"[INFO] Largest mask area_frac (raw): {float(areas[0]) / img_area:.3f}")

    kept_masks = []
    dropped_masks = []

    for i in range(masks.shape[0]):
        m = masks[i]
        is_bg, reason, stats = is_background_like(m)

        if DEBUG_PRINT_MASK_STATS:
            print(
                f"[DBG] mask {i}: "
                f"area={stats['area']} "
                f"area_frac={stats['area_frac']:.3f} "
                f"border_touch={stats['border_touch_frac']:.3f} "
                f"hole_frac={stats['hole_frac']:.3f} "
                f"=> {('DROP' if is_bg else 'KEEP')} ({reason})"
            )
        else:
            print(f"[DBG] mask {i}: {('DROP' if is_bg else 'KEEP')} ({reason})")

        if is_bg:
            dropped_masks.append(m)
        else:
            kept_masks.append(m)

    if len(kept_masks) == 0:
        print("[WARN] All masks dropped. Loosen thresholds:")
        print("       - increase BG_AREA_FRAC_TH (e.g., 0.55)")
        print("       - increase BORDER_TOUCH_FRAC_TH (e.g., 0.15)")
        print("       - disable hole heuristic (ENABLE_HOLE_HEURISTIC=False)")
        # Save debug visuals anyway
        union_all = np.maximum.reduce(masks) if masks.shape[0] else np.zeros((h, w), dtype=np.uint8)
        save_mask_png(union_all, out_dir / "objects_mask.png")
        rgba = np.dstack([rgb, (union_all * 255).astype(np.uint8)])
        Image.fromarray(rgba).save(out_dir / "objects_only_rgba.png")
        Image.fromarray(overlay_mask(rgb, union_all)).save(out_dir / "overlay.png")
        (out_dir / "objects_contour.json").write_text(json.dumps({
            "image_w": int(w), "image_h": int(h), "polygons": [],
            "notes": "All masks dropped as background/noise by heuristics."
        }, indent=2))
        return

    masks_kept = np.stack(kept_masks, axis=0)
    print(f"[INFO] Masks kept: {masks_kept.shape[0]}")

    # Union for visuals (kept only)
    keep_union = np.zeros((h, w), dtype=np.uint8)
    for m in masks_kept:
        keep_union = np.maximum(keep_union, m)

    save_mask_png(keep_union, out_dir / "objects_mask.png")

    rgba = np.dstack([rgb, (keep_union * 255).astype(np.uint8)])
    Image.fromarray(rgba).save(out_dir / "objects_only_rgba.png")

    Image.fromarray(overlay_mask(rgb, keep_union)).save(out_dir / "overlay.png")

    # Optional debug image: green=kept, red=dropped
    if DEBUG_SAVE_KEEP_DROP_IMAGES:
        drop_union = np.zeros((h, w), dtype=np.uint8)
        for m in dropped_masks:
            drop_union = np.maximum(drop_union, m)

        dbg = make_debug_keep_drop_image(rgb, keep_union, drop_union)
        Image.fromarray(dbg).save(out_dir / "debug_keep_drop.png")
        print("[SAVED] debug_keep_drop.png (green=kept, red=dropped)")

    # Contours per kept mask
    n = export_contours_from_masks(masks_kept, out_dir / "objects_contour.json", w, h)

    print("[INFO] Exported polygons:", n)
    print("[SAVED] objects_only_rgba.png, objects_mask.png, overlay.png, objects_contour.json")


if __name__ == "__main__":
    main()
