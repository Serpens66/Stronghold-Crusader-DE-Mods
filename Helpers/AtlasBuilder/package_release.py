from __future__ import annotations

import sys
import zipfile
from pathlib import Path


def main() -> int:
    root = Path(__file__).resolve().parent
    source = root / "dist" / "AtlasBuilder"
    destination = root / "dist" / "AtlasBuilder-portable-win64.zip"
    if not (source / "AtlasBuilder.exe").is_file():
        print(f"Missing packaged executable: {source / 'AtlasBuilder.exe'}", file=sys.stderr)
        return 1
    if destination.exists():
        destination.unlink()
    with zipfile.ZipFile(destination, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted(source.rglob("*")):
            if path.is_file():
                archive.write(path, Path("AtlasBuilder") / path.relative_to(source))
        archive.write(root / "USAGE.md", Path("AtlasBuilder") / "USAGE.md")
    print(destination)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
