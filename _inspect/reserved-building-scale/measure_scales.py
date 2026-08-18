from pathlib import Path

import cv2
import numpy as np


SCREENSHOT = Path(__file__).with_name("reference.png")
HELP_ROOT = Path(
    r"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
    r"\Stronghold Crusader Definitive Edition_Data\StreamingAssets\Help\Images"
)
HELP_IMAGES = (
    "ST08_Barracks.png",
    "ST08_Bedouin_Stockade.png",
    "ST08_Mercenary_Post.png",
    "ST24_Engineers_Guild.png",
    "ST25_Tunnellers_Guild.png",
    "ST28_Oil_Smelter.png",
)


def load_rgba(path: Path) -> np.ndarray:
    image = cv2.imread(str(path), cv2.IMREAD_UNCHANGED)
    if image is None or image.ndim != 3 or image.shape[2] != 4:
        raise RuntimeError(f"Expected RGBA image: {path}")
    return image


def measure_sift(template: np.ndarray, screenshot: np.ndarray):
    sift = cv2.SIFT_create(nfeatures=10000, contrastThreshold=0.02)
    screenshot_gray = cv2.cvtColor(screenshot[:, :, :3], cv2.COLOR_BGR2GRAY)
    template_gray = cv2.cvtColor(template[:, :, :3], cv2.COLOR_BGR2GRAY)
    template_mask = np.where(template[:, :, 3] >= 192, 255, 0).astype(np.uint8)

    template_points, template_descriptors = sift.detectAndCompute(
        template_gray, template_mask
    )
    screenshot_points, screenshot_descriptors = sift.detectAndCompute(
        screenshot_gray, None
    )
    matcher = cv2.BFMatcher(cv2.NORM_L2)
    pairs = matcher.knnMatch(template_descriptors, screenshot_descriptors, k=2)
    matches = [first for first, second in pairs if first.distance < 0.72 * second.distance]
    if len(matches) < 4:
        return None

    source = np.float32(
        [template_points[match.queryIdx].pt for match in matches]
    ).reshape(-1, 1, 2)
    target = np.float32(
        [screenshot_points[match.trainIdx].pt for match in matches]
    ).reshape(-1, 1, 2)
    affine, inliers = cv2.estimateAffinePartial2D(
        source,
        target,
        method=cv2.RANSAC,
        ransacReprojThreshold=3.0,
        maxIters=10000,
        confidence=0.999,
        refineIters=50,
    )
    if affine is None or inliers is None:
        return None

    scale = float(np.hypot(affine[0, 0], affine[0, 1]))
    angle = float(np.degrees(np.arctan2(affine[1, 0], affine[0, 0])))
    return {
        "keypoints": len(template_points),
        "matches": len(matches),
        "inliers": int(inliers.sum()),
        "scale": scale,
        "angle": angle,
        "x": float(affine[0, 2]),
        "y": float(affine[1, 2]),
    }


def main() -> None:
    screenshot = load_rgba(SCREENSHOT)
    print(f"screenshot={screenshot.shape[1]}x{screenshot.shape[0]}")
    for name in HELP_IMAGES:
        template = load_rgba(HELP_ROOT / name)
        result = measure_sift(template, screenshot)
        print(name, result)


if __name__ == "__main__":
    main()
