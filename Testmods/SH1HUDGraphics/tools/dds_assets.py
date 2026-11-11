"""Authoring-only BC7 conversion and independent Pillow decoding/QA.

DDS pixels are stored bottom-up and premultiplied for the Extender's raw upload.
All image values returned here are top-down, premultiplied RGBA. Composite them
with composite(), never with a second alpha multiplication via Image.paste().
"""

from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import time

from PIL import Image, ImageChops, ImageOps, ImageStat

ROOT = Path(__file__).resolve().parents[1]
OPTIONS = ("-f", "BC7_UNORM", "-m", "1", "-pmalpha", "-vflip", "--ignore-srgb", "-nogpu", "-bc", "x", "-dx10", "-y")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def file_hash(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def tool_info():
    executable = Path(os.environ.get("SHCDE_TEXCONV", ROOT.parent / ".tools/texconv/texconv.exe")).resolve()
    require(executable.is_file(), "texconv missing: place official texconv.exe in .tools/texconv or set SHCDE_TEXCONV")
    result = subprocess.run([str(executable), "--version"], capture_output=True, text=True, check=True,
                            creationflags=subprocess.CREATE_NO_WINDOW)
    record = {"executable": str(executable), "version": result.stdout.strip(), "sha256": file_hash(executable), "options": list(OPTIONS)}
    provenance = executable.with_name("download.json")
    if provenance.exists():
        downloaded = json.loads(provenance.read_text(encoding="utf-8-sig"))
        require(record["sha256"] == downloaded["sha256"], "texconv differs from its download record")
        record["sourceUrl"] = downloaded["sourceUrl"]
        record["release"] = downloaded["release"]
        record["officialDigest"] = downloaded.get("officialDigest")
    else:
        record["sourceUrl"] = "user-supplied via SHCDE_TEXCONV"
    return record


def inspect_header(data):
    require(len(data) >= 148 and data[:4] == b"DDS ", "Not a DX10 DDS")
    require(struct.unpack_from("<I", data, 4)[0] == 124, "Invalid DDS header size")
    require(struct.unpack_from("<I", data, 76)[0] == 32 and struct.unpack_from("<I", data, 80)[0] & 4 and data[84:88] == b"DX10", "Missing DX10 pixel format")
    height, width = struct.unpack_from("<II", data, 12)
    depth, mips = struct.unpack_from("<II", data, 24)
    caps2 = struct.unpack_from("<I", data, 112)[0]
    dxgi, dimension, misc, count, alpha = struct.unpack_from("<IIIII", data, 128)
    require(dxgi == 98 and dimension == 3 and count == 1, "DDS must be BC7_UNORM, 2D, array size 1")
    flags = struct.unpack_from("<I", data, 8)[0]
    require(flags & 0x1007 == 0x1007, "Missing required DDS header flags")
    require(depth in (0, 1) and not (flags & 0x800000) and caps2 == 0 and misc == 0, "Volume/cube DDS is unsupported")
    require(width > 0 and height > 0 and width % 4 == 0 and height % 4 == 0, "DDS dimensions must be positive multiples of four")
    require(mips == 1, "DDS must contain exactly one mip level")
    require(alpha in (2, 3), "DDS must declare premultiplied or fully opaque alpha")
    payload = width // 4 * (height // 4) * 16
    require(len(data) == 148 + payload, "DDS payload length differs from BC7 base level")
    return {"width": width, "height": height, "dxgiFormat": dxgi, "mipLevels": mips, "alphaMode": alpha, "payloadBytes": payload}


def decode(path):
    header = inspect_header(Path(path).read_bytes())
    with Image.open(path) as image:
        image.load()
        require(image.mode == "RGBA" and image.size == (header["width"], header["height"]), "DDS decoder dimensions/mode differ")
        if header["alphaMode"] == 3:
            require(image.getchannel("A").getextrema() == (255, 255), "Opaque DDS contains transparency")
        return image.transpose(Image.Transpose.FLIP_TOP_BOTTOM)


def premultiply(image):
    # Retag RGBa bytes as RGBA without unpremultiplying them.
    return Image.frombytes("RGBA", image.size, image.convert("RGBa").tobytes())


def composite(image, background):
    if isinstance(background, tuple):
        background = Image.new("RGB", image.size, background)
    require(background.size == image.size and background.mode == "RGB", "Invalid comparison background")
    inverse_alpha = ImageOps.invert(image.getchannel("A"))
    rgb = [ImageChops.add(channel, ImageChops.multiply(bg, inverse_alpha))
           for channel, bg in zip(image.split()[:3], background.split())]
    return Image.merge("RGB", rgb)


def errors(reference, decoded):
    require(reference.size == decoded.size, "DDS crop dimensions changed")
    stats = ImageStat.Stat(ImageChops.difference(reference, decoded))
    backgrounds = {}
    for name, background in (("black", (0, 0, 0)), ("white", (255, 255, 255))):
        difference = ImageChops.difference(composite(reference, background), composite(decoded, background))
        measured = ImageStat.Stat(difference)
        backgrounds[name] = {"meanAbsolute": [round(v, 5) for v in measured.mean], "rms": [round(v, 5) for v in measured.rms], "max": [v[1] for v in measured.extrema]}
    return {"meanAbsoluteRgba": [round(v, 5) for v in stats.mean], "rmsRgba": [round(v, 5) for v in stats.rms],
            "maxRgba": [v[1] for v in stats.extrema], "composited": backgrounds}


def convert(png, destination, tool, log_path):
    require(png.stem == destination.stem and destination.suffix == ".dds", "Conversion must preserve atlas stem")
    destination.parent.mkdir(parents=True, exist_ok=True)
    log_path.parent.mkdir(parents=True, exist_ok=True)
    command = [tool["executable"], *OPTIONS, "-o", str(destination.parent), str(png)]
    started = time.monotonic()
    with log_path.open("wb") as output:
        process = subprocess.Popen(command, stdout=output, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
        while True:
            try:
                code = process.wait(timeout=10)
                break
            except subprocess.TimeoutExpired:
                print(datetime.now().astimezone().isoformat(timespec="milliseconds") +
                      f" BC7 encoding {png.name}: {time.monotonic() - started:.0f}s, log={log_path.stat().st_size} bytes", flush=True)
    require(code == 0 and destination.is_file(), f"texconv failed for {png.name}; inspect {log_path}")
    header = inspect_header(destination.read_bytes())
    with Image.open(png) as source:
        require(source.size == (header["width"], header["height"]), "texconv resized the atlas")
    return {**header, "seconds": round(time.monotonic() - started, 3), "log": str(log_path)}


def conversion_probe(directory, tool):
    """Large constant BC7 blocks isolate alpha/orientation errors from lossy edges."""
    directory.mkdir(parents=True, exist_ok=True)
    source = Image.new("RGBA", (16, 8))
    quadrants = [((0, 0, 8, 4), (240, 80, 40, 128)), ((8, 0, 16, 4), (20, 200, 60, 255)),
                 ((0, 4, 8, 8), (40, 80, 240, 64)), ((8, 4, 16, 8), (200, 120, 40, 0))]
    for box, value in quadrants:
        source.paste(value, box)
    png, target = directory / "alpha-orientation.png", directory / "alpha-orientation.dds"
    source.save(png)
    conversion = convert(png, target, tool, directory / "texconv.log")
    decoded, reference = decode(target), premultiply(source)
    # Four homogeneous 4x4-aligned regions encode almost exactly. A tolerance of
    # three byte values is codec quantization, far below wrong-flip/double-alpha errors.
    for point in ((2, 1), (10, 1), (2, 5), (10, 5)):
        require(max(abs(a - b) for a, b in zip(reference.getpixel(point), decoded.getpixel(point))) <= 3,
                "BC7 probe failed: orientation, channels, gamma or alpha multiplication differs")
    return {"conversion": conversion, "errors": errors(reference, decoded), "passed": True}
