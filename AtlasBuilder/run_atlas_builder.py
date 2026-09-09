import sys

from atlas_builder.gm_groups import SUPPORTED_GROUPS
from atlas_builder.gui import run_gui


if __name__ == "__main__":
    # Used by release validation to prove the frozen application can load its bundled resources.
    if "--self-test" in sys.argv:
        if len(SUPPORTED_GROUPS) != 195 or "tile_ruins" not in SUPPORTED_GROUPS:
            raise SystemExit(1)
        raise SystemExit(0)
    run_gui()
