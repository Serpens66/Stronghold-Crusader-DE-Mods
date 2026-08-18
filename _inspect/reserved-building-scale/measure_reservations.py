from pathlib import Path

import cv2
import numpy as np


SCREENSHOT = Path(
    r"C:\Users\Serpens66\Pictures\Screenshots\Screenshot 2026-07-31 133034.jpeg"
)


def main() -> None:
    image = cv2.imread(str(SCREENSHOT), cv2.IMREAD_COLOR)
    if image is None:
        raise RuntimeError(f"Could not load {SCREENSHOT}")

    hsv = cv2.cvtColor(image, cv2.COLOR_BGR2HSV)
    for name, x, y in (
        ("barracks-yard", 540, 850),
        ("engineers-yard", 1070, 505),
        ("oil-yard", 1570, 515),
        ("mercenary-yard", 1760, 820),
        ("bedouin-yard", 1120, 1090),
        ("grass", 850, 700),
        ("grass-2", 1850, 350),
    ):
        patch = hsv[y - 5 : y + 6, x - 5 : x + 6]
        print(name, np.median(patch.reshape(-1, 3), axis=0))
    # Restrict each crop to the yard-facing half, avoiding most building pixels.
    regions = {
        "barracks": (70, 650, 850, 1030),
        "engineers": (780, 380, 1420, 680),
        "oil": (1320, 390, 1900, 700),
        "mercenary": (1250, 680, 2220, 1100),
        "bedouin": (650, 950, 1540, 1432),
    }
    for name, (left, top, right, bottom) in regions.items():
        crop = hsv[top:bottom, left:right]
        # Grass clusters around hue 36; tan yards remain below hue 30.
        yard = cv2.inRange(
            crop, np.array((4, 20, 45)), np.array((30, 230, 250))
        )
        yard = cv2.morphologyEx(
            yard, cv2.MORPH_CLOSE, np.ones((7, 7), np.uint8), iterations=1
        )
        count, _, stats, centroids = cv2.connectedComponentsWithStats(yard)
        candidates = []
        for index in range(1, count):
            x, y, width, height, area = stats[index]
            if area >= 1_000:
                candidates.append(
                    (area, x + left, y + top, width, height, *centroids[index])
                )
        print(name, sorted(candidates, reverse=True)[:8])


if __name__ == "__main__":
    main()
