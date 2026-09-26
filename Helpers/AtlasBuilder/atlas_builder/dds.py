"""BC7 DDS support for the Script Extender's bottom-up raw texture loader."""

from __future__ import annotations

import hashlib
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageOps


TEXCONV_VERSION = "may2026"
TEXCONV_SHA256 = "DCFDEC10244E02CF5037FBA089C55FB7E1326B1C8181742D77D15FA5CB5EEF06"
TEXCONV_URL = "https://github.com/microsoft/DirectXTex/releases/download/may2026/texconv.exe"
BC7_UNORM = 98


def texconv_path() -> Path:
    base = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent.parent))
    return base / "tools" / "texconv.exe"


def checked_texconv() -> Path:
    path = texconv_path()
    if not path.is_file():
        raise ValueError(f"BC7 encoder missing: {path}. Obtain the pinned DirectXTex {TEXCONV_VERSION} texconv.exe.")
    actual = hashlib.sha256(path.read_bytes()).hexdigest().upper()
    if actual != TEXCONV_SHA256:
        raise ValueError(f"BC7 encoder SHA-256 mismatch: {path}")
    return path


def _run_texconv(*arguments: str) -> None:
    command = [str(checked_texconv()), "-nologo", *arguments]
    try:
        result = subprocess.run(command, capture_output=True, text=True, timeout=3600, check=False)
    except (OSError, subprocess.TimeoutExpired) as exc:
        raise ValueError(f"Texconv could not complete: {exc}") from exc
    if result.returncode != 0:
        detail = (result.stderr or result.stdout).strip()
        raise ValueError(f"Texconv failed ({result.returncode}): {detail}")


def encode_bc7(source_png: Path, destination_dds: Path) -> None:
    """Keep the source PNG upright for JSON geometry, flip only the DDS payload."""
    _run_texconv(
        "-f", "BC7_UNORM", "-m", "1", "-nogpu", "-bc", "x", "-vflip",
        "-o", str(destination_dds.parent), str(source_png),
    )
    produced = destination_dds.parent / source_png.with_suffix(".dds").name
    if not produced.is_file():
        raise ValueError(f"Texconv did not produce {produced}")
    if produced != destination_dds:
        produced.replace(destination_dds)


def read_bc7_header(path: Path) -> tuple[int, int]:
    data = path.read_bytes()
    if len(data) < 148 or data[:4] != b"DDS " or struct.unpack_from("<I", data, 4)[0] != 124:
        raise ValueError(f"Invalid DDS header: {path}")
    height, width = struct.unpack_from("<II", data, 12)
    mip_count = struct.unpack_from("<I", data, 28)[0]
    fourcc = data[84:88]
    dxgi, resource_dimension, misc_flag, array_size, _misc_flags2 = struct.unpack_from("<IIIII", data, 128)
    if (not width or not height or width % 4 or height % 4 or fourcc != b"DX10"
            or dxgi != BC7_UNORM or resource_dimension != 3 or misc_flag != 0
            or array_size != 1 or mip_count != 1):
        raise ValueError(f"Unsupported BC7 DDS header: {path}")
    expected = 148 + (width // 4) * (height // 4) * 16
    if len(data) != expected:
        raise ValueError(f"BC7 DDS payload length differs: {path}")
    return width, height


def decode_bc7(path: Path) -> Image.Image:
    read_bc7_header(path)
    with tempfile.TemporaryDirectory(prefix="AtlasBuilder-DDS-check-", dir=path.parent) as temporary:
        directory = Path(temporary)
        _run_texconv("-ft", "png", "-o", str(directory), str(path))
        decoded = directory / path.with_suffix(".png").name
        if not decoded.is_file():
            raise ValueError(f"Texconv could not decode {path}")
        with Image.open(decoded) as image:
            return image.convert("RGBA")


def validate_bc7_pixels(
    path: Path, upright_source: Image.Image, upright_decoded: Image.Image | None = None
) -> dict[str, float]:
    """The raw DDS is flipped for Unity; compare its reconstructed upright image."""
    decoded = upright_decoded if upright_decoded is not None else ImageOps.flip(decode_bc7(path))
    expected = upright_source.convert("RGBA")
    if decoded.size != expected.size:
        raise ValueError(f"Decoded DDS dimensions differ: {path}")
    lhs = decoded.tobytes()
    rhs = expected.tobytes()
    errors = [abs(a - b) for a, b in zip(lhs, rhs)]
    mean_error = sum(errors) / len(errors)
    opposite = ImageOps.flip(expected).tobytes()
    opposite_mean = sum(abs(a - b) for a, b in zip(lhs, opposite)) / len(lhs)
    if opposite_mean + 1 < mean_error and opposite_mean < mean_error / 2:
        raise ValueError(f"DDS rows are not vertically flipped for Unity: {path}")
    return {"mean_error": mean_error, "maximum_error": float(max(errors))}
