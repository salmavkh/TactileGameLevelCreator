# Tactile Game Level Creator (TGLC)

Unity game project plus a local FastSAM segmentation pipeline used during capture.

## Repo Layout

- `unity/TactileGameLevelCreator/`: Unity project root (`Assets/`, `Packages/`, `ProjectSettings/`).
- `segmentation/fastsam_segmentation_for_unity.py`: FastSAM entry script called by Unity.
- `segmentation/models/`: place FastSAM weights here (not committed).
- `segmentation/archive/`: older script variants kept for reference.

Open Unity Hub using: `unity/TactileGameLevelCreator`.

## Segmentation Setup

1. Create a Python virtual environment inside `segmentation/`.
2. Install dependencies:
   - `ultralytics`
   - `opencv-python`
   - `pillow`
   - `numpy`
3. Download FastSAM weights (`FastSAM-x.pt`) and place it at:
   - `segmentation/models/FastSAM-x.pt`

The script expects weights at `./models/FastSAM-x.pt` relative to `segmentation/`.

## Unity Integration

`unity/TactileGameLevelCreator/Assets/Scripts/CaptureController.cs` runs the segmentation script via two inspector fields:

- `pythonExePath`
- `segmenterScriptPath`

In `unity/TactileGameLevelCreator/Assets/Scenes/Capture.unity`, defaults now point to:

- `.../segmentation/.venv/bin/python`
- `.../segmentation/fastsam_segmentation_for_unity.py`

If your machine paths differ, update those two fields in the Unity Inspector.

## Notes

- Model `.pt` files are gitignored.
- Runtime output folders under `segmentation/` are gitignored (`runs/`, `run_outputs/`, `test_run/`).
