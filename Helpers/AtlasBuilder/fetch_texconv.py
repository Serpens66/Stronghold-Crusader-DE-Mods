"""Fetch the pinned Microsoft DirectXTex encoder for development and packaging."""

from __future__ import annotations

import hashlib
import urllib.request
from pathlib import Path

from atlas_builder.dds import TEXCONV_SHA256, TEXCONV_URL


def main() -> None:
    destination = Path(__file__).resolve().parent / "tools" / "texconv.exe"
    if destination.is_file() and hashlib.sha256(destination.read_bytes()).hexdigest().upper() == TEXCONV_SHA256:
        print(f"Verified {destination}")
        return
    payload = urllib.request.urlopen(TEXCONV_URL, timeout=60).read()
    actual = hashlib.sha256(payload).hexdigest().upper()
    if actual != TEXCONV_SHA256:
        raise RuntimeError(f"Texconv SHA-256 mismatch: {actual}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(payload)
    print(f"Verified {destination}")


if __name__ == "__main__":
    main()
