"""Verify the upload contract and exercise the real converter/independent decoder."""

from pathlib import Path
import struct
import sys
import tempfile
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "tools"))
import dds_assets


def header_fixture():
    data = bytearray(148 + 16)
    data[:4] = b"DDS "
    for offset, value in ((4, 124), (8, 0x1007), (12, 4), (16, 4), (24, 1), (28, 1),
                          (76, 32), (80, 4), (108, 0x1000), (128, 98), (132, 3), (140, 1), (144, 2)):
        struct.pack_into("<I", data, offset, value)
    data[84:88] = b"DX10"
    return data


class DdsContractTests(unittest.TestCase):
    def test_accepts_single_level_bc7_premultiplied_2d_header(self):
        result = dds_assets.inspect_header(header_fixture())
        self.assertEqual((4, 4, 98, 1, 2, 16), tuple(result[k] for k in ("width", "height", "dxgiFormat", "mipLevels", "alphaMode", "payloadBytes")))

    def test_rejects_truncated_or_extra_mip_payload(self):
        fixture = header_fixture()
        for malformed in (fixture[:-1], fixture + bytes(16)):
            with self.assertRaisesRegex(ValueError, "payload length"):
                dds_assets.inspect_header(malformed)

    def test_rejects_wrong_format_mips_alpha_arrays_and_cube(self):
        for offset, value in ((128, 99), (28, 2), (144, 1), (140, 2), (136, 4), (112, 0x200)):
            fixture = header_fixture()
            struct.pack_into("<I", fixture, offset, value)
            with self.subTest(offset=offset, value=value), self.assertRaises(ValueError):
                dds_assets.inspect_header(fixture)

    def test_rejects_unaligned_dimensions_and_missing_header_flags(self):
        for offset, value in ((16, 5), (8, 0), (80, 0)):
            fixture = header_fixture()
            struct.pack_into("<I", fixture, offset, value)
            with self.subTest(offset=offset), self.assertRaises(ValueError):
                dds_assets.inspect_header(fixture)

    def test_composites_premultiplied_values_without_second_alpha_multiply(self):
        image = Image.new("RGBA", (1, 1), (100, 50, 25, 128))
        self.assertEqual((100, 50, 25), dds_assets.composite(image, (0, 0, 0)).getpixel((0, 0)))
        self.assertEqual((227, 177, 152), dds_assets.composite(image, (255, 255, 255)).getpixel((0, 0)))

    def test_premultiplication_preserves_alpha_and_zeroes_hidden_rgb(self):
        source = Image.new("RGBA", (2, 1))
        source.putpixel((0, 0), (200, 100, 50, 128))
        source.putpixel((1, 0), (255, 80, 10, 0))
        result = dds_assets.premultiply(source)
        self.assertEqual((100, 50, 25, 128), result.getpixel((0, 0)))
        self.assertEqual((0, 0, 0, 0), result.getpixel((1, 0)))

    def test_real_bc7_roundtrip_detects_orientation_channels_and_alpha(self):
        local = dds_assets.ROOT / ".local"
        local.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="dds-test-", dir=local) as directory:
            result = dds_assets.conversion_probe(Path(directory), dds_assets.tool_info())
        self.assertTrue(result["passed"])
        self.assertEqual(98, result["conversion"]["dxgiFormat"])


if __name__ == "__main__":
    unittest.main()
