"""Build/verify this asset-only mod; Python, Pillow and texconv are build tools only.

Sources/ contains original artwork. resources.json records the intended vanilla
canvas sizes, not a permanent game-version assumption: every build checks them
against the selected vanilla XAML. BepInEx/ and .local/ are generated outputs.
DDS pages use premultiplied alpha and bottom-up storage for the Extender's raw upload.
"""

import argparse
import copy
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys
import tempfile
import xml.etree.ElementTree as ET
from xml.sax.saxutils import quoteattr
import zipfile

from PIL import Image, ImageDraw
import dds_assets

ROOT = Path(__file__).resolve().parents[1]
XAML_NS = "http://schemas.microsoft.com/winfx/2006/xaml"
KEY = "{" + XAML_NS + "}Key"
PAGE_SIZE = 2048
GUTTER = 2
RESOURCE_PATH = "Assets/GUI/Sprites/UI-MasterAtlas.xaml"
GUID = "HUDGraphics_Serp"


def log(message):
    print(datetime.now().astimezone().isoformat(timespec="milliseconds") + " " + message, flush=True)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_text(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(text.replace("\r\n", "\n").replace("\n", "\r\n").encode("utf-8"))


def write_json(path, value):
    write_text(path, json.dumps(value, ensure_ascii=False, indent=2) + "\n")


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def vanilla_path(override=None):
    if override:
        return Path(override).resolve()
    baseline = ROOT.parent / "_inspect/CrusaderDE-Native-Baseline"
    current = read_json(baseline / "CURRENT.json")
    return baseline / current["semanticDirectory"] / "resources/xaml" / RESOURCE_PATH


def atlas_entries(path):
    entries = {}
    for element in ET.parse(path).getroot():
        if KEY in element.attrib:
            name = element.attrib[KEY]
            require(name not in entries, f"Duplicate vanilla key: {name}")
            entries[name] = element
    return entries


def require_anchor(entries):
    require("UI-MasterAtlas" in entries and entries["UI-MasterAtlas"].tag.endswith("}BitmapImage"), "Missing UI-MasterAtlas BitmapImage insertion anchor")


def load_spec(atlas):
    spec = read_json(ROOT / "resources.json")
    require(spec["schemaVersion"] == 1 and spec["atlasResourcePath"] == RESOURCE_PATH, "Unsupported resource map")
    entries = atlas_entries(atlas)
    require_anchor(entries)
    rows = spec["resources"]
    require(rows and len({r["key"] for r in rows}) == len(rows), "Empty/duplicate resource keys")
    require(len({r["file"].casefold() for r in rows}) == len(rows), "Duplicate PNG filenames")
    require(not (set(spec["preserveKeys"]) & {r["key"] for r in rows}), "A preserved key is replaced")
    expected_files = {r["file"] for r in rows}
    require({p.name for p in (ROOT / "Sources").iterdir()} == expected_files, "Sources and resource map differ")
    for row in rows:
        name = row["key"]
        require(row["file"] == name + ".png" and Path(row["file"]).name == row["file"], f"Unsafe PNG filename: {name}")
        require("'" not in name, f"Unsupported XPath key: {name}")
        require(row["alignment"] in ("center", "bottom-center"), f"Unknown alignment: {name}")
        require(name in entries and entries[name].tag.endswith("}CroppedBitmap"), f"Not a vanilla cropped bitmap: {name}")
        rect = [int(v) for v in entries[name].attrib["SourceRect"].split(",")]
        require(rect[2:] == [row["width"], row["height"]], f"Vanilla dimensions changed; review resource map: {name}")
        require(0 < row["width"] <= PAGE_SIZE - GUTTER * 2 and 0 < row["height"] <= PAGE_SIZE - GUTTER * 2, f"Image exceeds page size: {name}")
        with Image.open(ROOT / "Sources" / row["file"]) as source:
            source.load()
            require(source.mode == "RGBA", f"Source must retain RGBA: {name}")
    for name in spec["preserveKeys"]:
        require(name in entries, f"Missing preserved vanilla key: {name}")
    return spec, entries


def fit(source, target_size, alignment):
    """Fit the complete source canvas; never crop the artwork or stretch its ratio."""
    require(alignment in ("center", "bottom-center"), "Unknown alignment")
    width, height = target_size
    if source.size == target_size:
        return source.copy(), (0, 0, width, height)
    scale = min(width / source.width, height / source.height)
    size = (min(width, max(1, int(source.width * scale + 0.5))), min(height, max(1, int(source.height * scale + 0.5))))
    # Pillow resamples RGBA via premultiplied alpha, then returns straight RGBA.
    resized = source.resize(size, Image.Resampling.LANCZOS)
    x = (width - size[0]) // 2
    y = height - size[1] if alignment == "bottom-center" else (height - size[1]) // 2
    canvas = Image.new("RGBA", target_size)
    canvas.paste(resized, (x, y))
    return canvas, (x, y, *size)


def normalize(source, row):
    """Keep related states registered; corrections are explicit in resources.json."""
    correction = row.get("normalization", {})
    image = source
    if "sourceCrop" in correction:
        box = tuple(correction["sourceCrop"])
        left, top, right, bottom = box
        require(0 <= left < right <= image.width and 0 <= top < bottom <= image.height, "Invalid transparent source crop")
        opaque = image.getchannel("A").getbbox()
        require(opaque is None or (left <= opaque[0] and top <= opaque[1] and right >= opaque[2] and bottom >= opaque[3]), "Source crop would remove visible pixels")
        image = image.crop(box)
    if "sourceCanvas" in correction:
        virtual = Image.new("RGBA", tuple(correction["sourceCanvas"]))
        offset = tuple(correction["sourceOffset"])
        require(contains((0, 0, *virtual.size), (*offset, *image.size)), "Source registration outside virtual canvas")
        virtual.paste(image, offset)
        image = virtual
    target = (row["width"], row["height"])
    fit_size = tuple(correction.get("fitSize", target))
    result, rect = fit(image, fit_size, row["alignment"])
    if fit_size != target:
        canvas = Image.new("RGBA", target)
        offset = tuple(correction["targetOffset"])
        require(contains((0, 0, *target), (*offset, *fit_size)), "Registered fit outside target canvas")
        canvas.paste(result, offset)
        rect = (rect[0] + offset[0], rect[1] + offset[1], *rect[2:])
        result = canvas
    return result, rect


def contains(outer, inner):
    x, y, w, h = outer
    ix, iy, iw, ih = inner
    return x <= ix and y <= iy and x + w >= ix + iw and y + h >= iy + ih


def intersects(a, b):
    x, y, w, h = a
    bx, by, bw, bh = b
    return x < bx + bw and bx < x + w and y < by + bh and by < y + h


def occupy(free, used):
    """MaxRects free-space splitting. Frames are never rotated."""
    x, y, w, h = used
    result = []
    for rectangle in free:
        fx, fy, fw, fh = rectangle
        if not intersects(rectangle, used):
            result.append(rectangle)
            continue
        if x > fx:
            result.append((fx, fy, x - fx, fh))
        if x + w < fx + fw:
            result.append((x + w, fy, fx + fw - x - w, fh))
        if y > fy:
            result.append((fx, fy, fw, y - fy))
        if y + h < fy + fh:
            result.append((fx, y + h, fw, fy + fh - y - h))
    unique = list(dict.fromkeys(result))
    return [a for a in unique if not any(a != b and contains(b, a) for b in unique)]


def pack(rows, page_size=PAGE_SIZE):
    """Select the smallest result of four deterministic orderings, with 2px gutters."""
    orders = (
        lambda r: (-r["width"] * r["height"], r["key"]),
        lambda r: (-r["width"], -r["height"], r["key"]),
        lambda r: (-r["height"], -r["width"], r["key"]),
        lambda r: (-max(r["width"], r["height"]), -min(r["width"], r["height"]), r["key"]),
    )
    candidates = []
    for order in orders:
        pages, frames = [], {}
        for row in sorted(rows, key=order):
            w, h = row["width"] + GUTTER * 2, row["height"] + GUTTER * 2
            require(w <= page_size and h <= page_size, "Frame exceeds atlas page")
            choices = []
            for index, page in enumerate(pages):
                for x, y, fw, fh in page["free"]:
                    if w <= fw and h <= fh:
                        choices.append((min(fw - w, fh - h), max(fw - w, fh - h), index, y, x))
            if choices:
                _, _, index, y, x = min(choices)
            else:
                index, x, y = len(pages), 0, 0
                pages.append({"free": [(0, 0, page_size, page_size)], "width": 0, "height": 0})
            page = pages[index]
            page["free"] = occupy(page["free"], (x, y, w, h))
            page["width"] = max(page["width"], x + w)
            page["height"] = max(page["height"], y + h)
            frames[row["key"]] = {"page": index, "rect": [x + GUTTER, y + GUTTER, row["width"], row["height"]]}
        sizes = [((p["width"] + 3) // 4 * 4, (p["height"] + 3) // 4 * 4) for p in pages]
        candidates.append((sum(w * h for w, h in sizes), len(sizes), sizes, frames))
    _, _, sizes, frames = min(candidates, key=lambda candidate: candidate[:2])
    return sizes, frames


def add_gutter(page, image, x, y):
    """Extrude edge pixels outside SourceRect to isolate bilinear filtering."""
    w, h = image.size
    page.paste(image, (x, y))
    page.paste(image.crop((0, 0, w, 1)).resize((w, GUTTER)), (x, y - GUTTER))
    page.paste(image.crop((0, h - 1, w, h)).resize((w, GUTTER)), (x, y + h))
    page.paste(image.crop((0, 0, 1, h)).resize((GUTTER, h)), (x - GUTTER, y))
    page.paste(image.crop((w - 1, 0, w, h)).resize((GUTTER, h)), (x + w, y))
    for sx, sy, dx, dy in ((0, 0, -GUTTER, -GUTTER), (w - 1, 0, w, -GUTTER), (0, h - 1, -GUTTER, h), (w - 1, h - 1, w, h)):
        page.paste(image.getpixel((sx, sy)), (x + dx, y + dy, x + dx + GUTTER, y + dy + GUTTER))


def page_key(index):
    return f"{GUID}.Atlas{index + 1:02d}"


def page_filename(index):
    return f"atlas-{index + 1:02d}.dds"


def patch_text(rows, sizes, frames):
    lines = ['<?xml version="1.0" encoding="utf-8"?>', f'<Patch xmlns:x="{XAML_NS}">']
    # InsertAfter puts sources before every crop, avoiding forward StaticResource references.
    for index in reversed(range(len(sizes))):
        lines.extend([
            '  <Operation Type="InsertAfter" XPath="//*[@x:Key=\'UI-MasterAtlas\']">',
            '    <Content>',
            f'      <BitmapImage x:Key="{page_key(index)}" UriSource="{GUID}/{page_filename(index)}" />',
            '    </Content>', '  </Operation>',
        ])
    for row in rows:
        frame = frames[row["key"]]
        rect = ",".join(str(n) for n in frame["rect"])
        xpath = "//*[@x:Key='" + row["key"] + "']"
        lines.extend([
            f'  <Operation Type="Replace" XPath={quoteattr(xpath)}>', '    <Content>',
            f'      <CroppedBitmap x:Key={quoteattr(row["key"])} Source="{{StaticResource {page_key(frame["page"])}}}" SourceRect="{rect}" />',
            '    </Content>', '  </Operation>',
        ])
    return "\n".join(lines + ['</Patch>', ''])


def compatibility(entries, rows):
    """Inspect the concrete atlas asset/patch paths, not unrelated gameplay code."""
    results = []
    providers = {
        "ScriptExtender": ROOT.parent / "shcde-script-extender/deps",
        "Fixes": ROOT.parent.parent / "Fremde Mods/shcde-fixes-main/deps",
    }
    for name, folder in providers.items():
        record = {"provider": name, "path": str(folder), "available": folder.is_dir()}
        if not folder.is_dir():
            record["status"] = "not-checked"
            log(f"NOTE: Compatibility source unavailable: {folder}")
            results.append(record)
            continue
        override = folder / "Override" / RESOURCE_PATH
        if override.exists():
            other = atlas_entries(override)
            require_anchor(other)
            for row in rows:
                key = row["key"]
                # Atlas packing coordinates, formatting and provider texture names may change.
                # Only the existing key/type/logical dimensions matter to our replacement.
                require(key in other and other[key].tag == entries[key].tag, f"{name} removes/changes resource type: {key}")
                require([int(n) for n in other[key].attrib["SourceRect"].split(",")[2:]] == [row["width"], row["height"]], f"{name} changes logical image dimensions: {key}")
            record["overridePath"] = str(override)
            record["overrideSha256"] = digest(override)
        patch = folder / "Patches" / RESOURCE_PATH
        record["atlasPatchPresent"] = patch.exists()
        record["status"] = "review-required" if patch.exists() else "checked"
        if patch.exists():
            # Do not turn today's absence into a permanent ban on future provider patches.
            record["atlasPatchSha256"] = digest(patch)
            log(f"NOTE: Additional atlas patch needs a combined review: {patch}")
        record["atlasOverridesMatch"] = override.exists()
        results.append(record)
    return results


def apply_emitted_patch(entries, patch):
    """Check the exact emitted XPath subset; this is not a Noesis runtime test."""
    result = [copy.deepcopy(element) for element in entries.values()]
    for operation in patch:
        match = re.fullmatch(r"//\*\[@x:Key='([^']+)'\]", operation.attrib["XPath"])
        require(match is not None, "Unexpected generated XPath")
        matches = [i for i, element in enumerate(result) if element.get(KEY) == match[1]]
        require(len(matches) == 1, f"Generated XPath must match exactly once: {operation.attrib['XPath']}")
        index = matches[0]
        content = operation.find("Content")
        require(content is not None and len(content) == 1, "Invalid patch Content")
        replacement = copy.deepcopy(content[0])
        if operation.attrib["Type"] == "InsertAfter":
            result.insert(index + 1, replacement)
        elif operation.attrib["Type"] == "Replace":
            result[index] = replacement
        else:
            raise ValueError("Unexpected generated operation")
    return result


def validate_resolution(entries, patch, expected):
    combined = apply_emitted_patch(entries, patch)
    combined_keys = [element.get(KEY) for element in combined]
    require(len(set(combined_keys)) == len(combined_keys), "Duplicate resource key after patch application")
    declared = set()
    for element in combined:
        source = element.get("Source", "")
        if source.startswith("{StaticResource " + GUID + ".Atlas"):
            require(source[len("{StaticResource "):-1] in declared, "Merged crop references an undeclared atlas")
        declared.add(element.get(KEY))
        key = element.get(KEY)
        if key in entries and key not in expected:
            require(ET.tostring(element) == ET.tostring(entries[key]), f"Unrelated vanilla resource changed: {key}")


def validate_package(package, spec, entries):
    metadata = read_json(package / "info.json")
    require(metadata == read_json(ROOT / "info.json"), "Packaged metadata differs")
    require(metadata["GUID"] == GUID and metadata["Manifest"] == 0 and metadata["NetworkMode"] == 0 and metadata["AssetMode"] == "Global", "Not a client-side asset-only mod")
    require(metadata["Dependencies"] == [{"GUID": "000shcdese"}], "Unexpected runtime dependency")
    patch = ET.parse(package / "Patches" / RESOURCE_PATH).getroot()
    seen, sources, replacements, occupied, quality = set(entries), {}, set(), {}, {}
    expected = {r["key"]: r for r in spec["resources"]}
    actual_files = {p.relative_to(package).as_posix() for p in package.rglob("*") if p.is_file()}
    expected_files = {"info.json", "Patches/" + RESOURCE_PATH}
    try:
        validate_resolution(entries, patch, expected)
        for operation in patch:
            content = operation.find("Content")
            require(content is not None and len(content) == 1, "Patch Content must have exactly one root")
            element = content[0]
            key = element.attrib[KEY]
            if operation.attrib["Type"] == "InsertAfter":
                require(operation.attrib["XPath"] == "//*[@x:Key='UI-MasterAtlas']" and element.tag == "BitmapImage", "Invalid source insertion")
                require(key.startswith(GUID + ".Atlas") and key not in seen, "Atlas resource key collision")
                uri = element.attrib["UriSource"]
                require(re.fullmatch(GUID + r"/atlas-\d{2}\.dds", uri), "Invalid atlas URI")
                relative = "Override/Assets/GUI/Sprites/" + uri
                image = dds_assets.decode(package / relative)
                require(max(image.size) <= PAGE_SIZE, "Invalid DDS atlas dimensions")
                sources[key] = image
                occupied[key] = []
                expected_files.add(relative)
                seen.add(key)
            else:
                require(operation.attrib["Type"] == "Replace" and element.tag == "CroppedBitmap" and key in expected and key not in replacements, "Invalid/duplicate replacement")
                require(operation.attrib["XPath"] == "//*[@x:Key='" + key + "']", "Incorrect replacement target")
                row = expected[key]
                require(key in entries and key not in spec["preserveKeys"], "Nonexistent/preserved patch target")
                source_key = element.attrib["Source"].removeprefix("{StaticResource ").removesuffix("}")
                require(source_key in sources, "Atlas source used before declaration")
                x, y, w, h = [int(n) for n in element.attrib["SourceRect"].split(",")]
                require([w, h] == [row["width"], row["height"]], "Changed logical image dimensions")
                padded = (x - GUTTER, y - GUTTER, w + GUTTER * 2, h + GUTTER * 2)
                require(contains((0, 0, *sources[source_key].size), padded), "Crop/gutter outside atlas")
                require(not any(intersects(padded, other) for other in occupied[source_key]), "Overlapping atlas frames/gutters")
                occupied[source_key].append(padded)
                with Image.open(ROOT / "Sources" / row["file"]) as original:
                    normalized, _ = normalize(original, row)
                    crop = sources[source_key].crop((x, y, x + w, y + h))
                    measured = dds_assets.errors(dds_assets.premultiply(normalized), crop)
                    # A generous corruption guard, not a substitute for the visual review.
                    # The lossy BC7 error itself is recorded per crop on black AND white.
                    require(measured["meanAbsoluteRgba"][3] <= 3 and
                            all(max(background["meanAbsolute"]) <= 8 for background in measured["composited"].values()),
                            f"Excessive BC7 difference; inspect quality report: {key}")
                    quality[key] = measured
                replacements.add(key)
        require(replacements == set(expected), "Missing replacement resources")
        require(actual_files == expected_files, "Unexpected/missing package files")
    finally:
        for image in sources.values():
            image.close()
    return len(replacements), len(sources), quality


def clean_generated_package(package):
    expected = (ROOT / "BepInEx/plugins" / GUID).resolve()
    require(package.resolve() == expected and expected.is_relative_to(ROOT), "Unsafe generated output path")
    if package.exists():
        require(not package.is_symlink() and all(not p.is_symlink() and p.resolve().is_relative_to(expected) for p in package.rglob("*")), "Linked output cannot be replaced")
        require(read_json(package / "info.json")["GUID"] == GUID, "Refusing to replace unrelated output")
        shutil.rmtree(package)


def previews(package, result):
    selected = [
        "UI-HUD 000", "Panel_Border_Bottom", "Panel_Corner_Top_Left",
        "UI-Buildings G001", "UI-Buildings G002", "UI-Buildings K005", "UI-Buildings K006",
        "Resources_Normal", "Resources_Highlight", "Resources_Disabled",
        "Scribe_B_up", "Scribe_B_over", "Scribe_B_selected",
    ]
    frames = result["frames"]
    directory = package / "Override/Assets/GUI/Sprites" / GUID
    pages = [dds_assets.decode(directory / page_filename(i)) for i in range(result["atlasCount"])]
    sheet = Image.new("RGB", (1440, 1000), (30, 34, 39))
    draw = ImageDraw.Draw(sheet)
    draw.text((20, 14), "HUD Graphics - generated atlas crops (offline preview)", fill="white")
    for i, name in enumerate(selected):
        frame = frames[name]
        x, y, w, h = frame["rect"]
        crop = pages[frame["page"]].crop((x, y, x + w, y + h))
        if i < 2:
            left, top, max_width, max_height = 20, 48 + i * 180, 1380, 145
        else:
            cell = i - 2
            left, top, max_width, max_height = 20 + cell % 6 * 235, 410 + cell // 6 * 285, 215, 240
        draw.text((left, top), f"{name} ({w}x{h})", fill="white")
        scale = min(max_width / w, max_height / h, 1)
        size = (max(1, round(w * scale)), max(1, round(h * scale)))
        background = Image.new("RGB", crop.size)
        checker = ImageDraw.Draw(background)
        for cy in range(0, crop.height, 12):
            for cx in range(0, crop.width, 12):
                shade = 72 if (cx // 12 + cy // 12) % 2 else 96
                checker.rectangle((cx, cy, cx + 11, cy + 11), fill=(shade, shade, shade))
        rendered = dds_assets.composite(crop, background)
        if size != crop.size:
            rendered = rendered.resize(size, Image.Resampling.LANCZOS)
        sheet.paste(rendered, (left, top + 22))
    sheet.save(ROOT / ".local/preview.png")
    overview = Image.new("RGB", (3 * 460, ((len(pages) + 2) // 3) * 490), (30, 34, 39))
    draw = ImageDraw.Draw(overview)
    for i, page in enumerate(pages):
        x, y = i % 3 * 460, i // 3 * 490
        draw.text((x + 8, y + 8), f"Page {i + 1}: {page.width}x{page.height}", fill="white")
        thumbnail = dds_assets.composite(page, (30, 34, 39))
        thumbnail.thumbnail((448, 448))
        overview.paste(thumbnail, (x + 6, y + 32))
        page.close()
    overview.save(ROOT / ".local/atlas-overview.png")


def comparison_preview(package, result, rows):
    selected = ["UI-HUD 000", "Panel_Border_Bottom", "Panel_Corner_Top_Left",
                "UI-Buildings G001", "UI-Buildings G002", "UI-Buildings K006",
                "Resources_Disabled", "Scribe_B_over", "UI-Scribe B001"]
    directory = package / "Override/Assets/GUI/Sprites" / GUID
    pages = [dds_assets.decode(directory / page_filename(i)) for i in range(result["atlasCount"])]
    by_key = {row["key"]: row for row in rows}
    sheet = Image.new("RGB", (1440, len(selected) * 220 + 40), (45, 48, 52))
    draw = ImageDraw.Draw(sheet)
    for column, label in enumerate(("Original / black", "BC7 / black", "Original / white", "BC7 / white")):
        draw.text((column * 360 + 8, 8), label, fill="white")
    for index, name in enumerate(selected):
        row, frame = by_key[name], result["frames"][name]
        x, y, w, h = frame["rect"]
        with Image.open(ROOT / "Sources" / row["file"]) as source:
            normalized, _ = normalize(source, row)
        original = dds_assets.premultiply(normalized)
        decoded = pages[frame["page"]].crop((x, y, x + w, y + h))
        top = index * 220 + 36
        for column, (image, background) in enumerate(((original, (0, 0, 0)), (decoded, (0, 0, 0)),
                                                      (original, (255, 255, 255)), (decoded, (255, 255, 255)))):
            rendered = dds_assets.composite(image, background)
            rendered.thumbnail((344, 190), Image.Resampling.LANCZOS)
            draw.text((column * 360 + 8, top), name, fill="white")
            sheet.paste(rendered, (column * 360 + 8, top + 20))
    for page in pages:
        page.close()
    sheet.save(ROOT / ".local/bc7-comparison.png")


def build(atlas):
    spec, entries = load_spec(atlas)
    external = compatibility(entries, spec["resources"])
    sizes, frames = pack(spec["resources"])
    log(f"Packing {len(frames)} images into {len(sizes)} BC7-DDS pages (max {PAGE_SIZE}px)")
    local = ROOT / ".local"
    require(local.resolve().is_relative_to(ROOT), "Unsafe local artifact directory")
    local.mkdir(exist_ok=True)
    tool = dds_assets.tool_info()
    run_directory = Path(tempfile.mkdtemp(prefix="bc7-", dir=local))
    probe = dds_assets.conversion_probe(run_directory / "probe", tool)
    log("PASS: independent DDS decoder confirms orientation, channels and premultiplied alpha")
    conversions = []
    with tempfile.TemporaryDirectory(prefix="build-", dir=local) as temporary:
        package = Path(temporary) / GUID
        images_dir = package / "Override/Assets/GUI/Sprites" / GUID
        images_dir.mkdir(parents=True)
        png_dir = Path(temporary) / "png"
        png_dir.mkdir()
        for index, size in enumerate(sizes):
            page = Image.new("RGBA", size)
            for row in spec["resources"]:
                frame = frames[row["key"]]
                if frame["page"] != index:
                    continue
                with Image.open(ROOT / "Sources" / row["file"]) as source:
                    normalized, fitted = normalize(source, row)
                    add_gutter(page, normalized, *frame["rect"][:2])
                    frame["sourceSize"], frame["fittedRect"] = list(source.size), list(fitted)
            png = png_dir / f"atlas-{index + 1:02d}.png"
            page.save(png, compress_level=9)
            page.close()
            log(f"Encoding atlas {index + 1}/{len(sizes)}: {size[0]}x{size[1]}")
            conversions.append(dds_assets.convert(png, images_dir / page_filename(index), tool, run_directory / f"atlas-{index + 1:02d}.log"))
            log(f"Atlas {index + 1}/{len(sizes)} saved as BC7 in {conversions[-1]['seconds']}s")
        shutil.copyfile(ROOT / "info.json", package / "info.json")
        write_text(package / "Patches" / RESOURCE_PATH, patch_text(spec["resources"], sizes, frames))
        count, pages, quality = validate_package(package, spec, entries)
        patch = ET.parse(package / "Patches" / RESOURCE_PATH).getroot()
        for provider in external:
            if "overridePath" in provider:
                validate_resolution(atlas_entries(Path(provider["overridePath"])), patch, {r["key"] for r in spec["resources"]})
        destination = ROOT / "BepInEx/plugins" / GUID
        clean_generated_package(destination)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copytree(package, destination)
    metadata = read_json(ROOT / "info.json")
    archive = local / f"{GUID}-{metadata['Version']}.zip"
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as output:
        for file in sorted(destination.rglob("*")):
            if file.is_file():
                relative = "BepInEx/plugins/" + GUID + "/" + file.relative_to(destination).as_posix()
                entry = zipfile.ZipInfo(relative, date_time=(2000, 1, 1, 0, 0, 0))
                entry.compress_type = zipfile.ZIP_DEFLATED
                entry.external_attr = 0o100644 << 16
                output.writestr(entry, file.read_bytes())
    hashes = {p.relative_to(destination).as_posix(): digest(p) for p in sorted(destination.rglob("*")) if p.is_file()}
    with zipfile.ZipFile(archive) as output:
        prefix = "BepInEx/plugins/" + GUID + "/"
        require(len(output.namelist()) == len(hashes), "ZIP file count differs")
        for relative, expected_hash in hashes.items():
            require(hashlib.sha256(output.read(prefix + relative)).hexdigest() == expected_hash, f"ZIP mismatch: {relative}")
    result = {
        "observedUtc": datetime.now(timezone.utc).isoformat(), "version": metadata["Version"],
        "vanillaAtlas": str(atlas), "vanillaAtlasSha256": digest(atlas), "compatibility": external,
        "resourceCount": count, "atlasCount": pages, "atlasSizes": sizes,
        "atlasRgbaMiB": round(sum(w * h * 4 for w, h in sizes) / 1024 ** 2, 2),
        "atlasBc7MiB": round(sum(item["payloadBytes"] for item in conversions) / 1024 ** 2, 2),
        "format": "BC7_UNORM", "converter": tool, "conversions": conversions, "conversionProbe": probe,
        "compressionErrors": quality,
        "frames": frames, "sourceHashes": {r["file"]: digest(ROOT / "Sources" / r["file"]) for r in spec["resources"]},
        "buildInputHashes": {p.relative_to(ROOT).as_posix(): digest(p) for p in [ROOT / "info.json", ROOT / "resources.json", ROOT / "build.bat", *sorted((ROOT / "tools").glob("*")), *sorted((ROOT / "tests").glob("*.py"))] if p.is_file()},
        "package": str(destination), "packageHashes": hashes,
        "zip": str(archive), "zipSha256": digest(archive), "installed": False, "inGameTested": False,
    }
    write_json(local / "build.json", result)
    write_json(run_directory / "build.json", result)
    previews(destination, result)
    comparison_preview(destination, result, spec["resources"])
    log(f"PASS: {count} decoded crops, {pages} BC7 pages, {len(hashes)} package/ZIP hashes; no installation")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("build", "verify"))
    parser.add_argument("--vanilla-atlas", help="Optional explicitly selected vanilla UI-MasterAtlas.xaml")
    arguments = parser.parse_args()
    for file in (ROOT / "info.json", ROOT / "resources.json", ROOT / "build.bat", ROOT / ".gitignore", *sorted((ROOT / "tools").glob("*.py")), *sorted((ROOT / "tests").glob("*.py"))):
        require(not re.search(rb"(?<!\r)\n", file.read_bytes()), f"Text must use CRLF: {file}")
    atlas = vanilla_path(arguments.vanilla_atlas)
    if arguments.command == "build":
        build(atlas)
    else:
        spec, entries = load_spec(atlas)
        compatibility(entries, spec["resources"])
        count, pages, _ = validate_package(ROOT / "BepInEx/plugins" / GUID, spec, entries)
        log(f"PASS: {count} replacement crops on {pages} BC7 atlas pages")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, KeyError, ET.ParseError) as error:
        log("FAIL: " + str(error))
        sys.exit(1)
