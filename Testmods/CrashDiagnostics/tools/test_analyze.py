"""Behavior tests for untrusted binary boundaries and temporal attribution."""
import json
import pathlib
import struct
import tempfile
import unittest

import analyze


def native_blob():
    data = bytearray(analyze.LAYOUT["shared"])
    struct.pack_into("<QIIQII", data, 0, 0x3147414944434853, 1, 28832, len(data), 123, 64)
    struct.pack_into("<QQQQ", data, 32, 100, 200, 300, 10000000)
    return data


def map_record(sequence=1, address=0x1000, length=0x1000, kind=2, method=17, qpc=300):
    return {"sequence": sequence, "qpc": qpc, "address": address, "length": length,
            "method": method, "kind": kind, "truncated": 0, "name": "Example.Compute(int)",
            "image": "Example.dll", "sha256": ""}


def pack_map(data, record):
    offset = analyze.LAYOUT["mapsOffset"] + ((record["sequence"] - 1) % 8192) * 768
    struct.pack_into("<QQQQQQII", data, offset, record["sequence"], record["sequence"], record["qpc"], record["address"], record["length"], record["method"], record["kind"], 0)
    data[offset + 56:offset + 56 + len(record["name"])] = record["name"].encode()
    data[offset + 568:offset + 568 + len(record["image"])] = record["image"].encode()


def capture(data, seq=1, qpc=500):
    slot = analyze.LAYOUT["capturesOffset"] + ((seq - 1) % 16) * analyze.LAYOUT["capture"]
    struct.pack_into("<QQIIQQIIIIQQQ", data, slot, seq, seq, 0, 9408, qpc, 250, 12, 1, 4, 0, 0, 1, 0)
    ex = slot + analyze.LAYOUT["exceptionOffset"]
    struct.pack_into("<IIQQIIQQ", data, ex, 0xc0000005, 0, 0, 0x1000, 2, 0, 0, 0)
    struct.pack_into("<Q", data, slot + analyze.LAYOUT["contextOffset"] + 248, 0x1000)
    struct.pack_into("<QIIIIIIII", data, slot + analyze.LAYOUT["rangesOffset"], 0x1000, 4, 4, 0, 0, 1, 0x20, 0x1000, 0x20000)
    data[slot + 9408:slot + 9412] = b"\x48\x8b\x00\xc3"
    struct.pack_into("<Q", data, 64, seq)
    struct.pack_into("<Q", data, 96, 1)
    return slot


def compact_journal(records):
    data = bytearray(struct.pack("<8sIIQ", b"CDMAPS2\0", 2, 0, 0))
    for row in records:
        strings = [row[field].encode("utf-8") for field in ("name", "image", "sha256")]
        data.extend(struct.pack("<QQQQQQIIHHHH", row["sequence"], row["sequence"], row["qpc"], row["address"], row["length"],
                                row["method"], row["kind"], row["truncated"], *(len(value) for value in strings), 0))
        data.extend(b"".join(strings))
    return data


class AnalyzerTests(unittest.TestCase):
    def test_empty_minidump_does_not_invent_exception(self):
        data = struct.pack("<4sIIIIIQ", b"MDMP", 0xa793, 0, 32, 0, 0, 0)
        result = analyze.minidump(analyze.Blob(data))
        self.assertNotIn("exception", result)
        self.assertIn("No exception stream", result["limitations"])

    def test_minidump_directory_outside_file_rejected(self):
        data = struct.pack("<4sIIIIIQ", b"MDMP", 0xa793, 1, 0xffffffff, 0, 0, 0)
        with self.assertRaises(analyze.FormatError):
            analyze.minidump(analyze.Blob(data))

    def test_short_or_oversized_module_list_rejected(self):
        for module_count in (1, 0xffffffff):
            data = struct.pack("<4sIIIIIQ", b"MDMP", 0xa793, 1, 32, 0, 0, 0) + struct.pack("<III", 4, 4, 44) + struct.pack("<I", module_count)
            with self.assertRaises(analyze.FormatError):
                analyze.minidump(analyze.Blob(data))

    def test_duplicate_stream_rejected(self):
        data = struct.pack("<4sIIIIIQ", b"MDMP", 0xa793, 2, 32, 0, 0, 0) + struct.pack("<IIIIII", 4, 4, 56, 4, 4, 56) + b"\0" * 4
        with self.assertRaises(analyze.FormatError):
            analyze.minidump(analyze.Blob(data))

    def test_native_fault_bytes_and_named_mapping(self):
        data = native_blob()
        pack_map(data, map_record())
        capture(data)
        event = analyze.emergency(analyze.Blob(data))["events"][0]
        self.assertEqual(event["exception"]["accessAddress"], "0x0")
        self.assertTrue(event["faultCodeCaptured"])
        self.assertEqual(event["addressMappings"][0]["kind"], "named-jit-method")
        self.assertEqual(event["status"], ["exception_observed"])

    def test_partial_record_is_not_presented_as_complete(self):
        data = native_blob()
        slot = capture(data)
        struct.pack_into("<Q", data, slot + 8, 0)
        result = analyze.emergency(analyze.Blob(data))
        self.assertEqual(result["events"], [])
        self.assertEqual(result["counters"]["partialCaptureSlots"], 1)
        self.assertEqual(result["counters"]["unavailableEarlierCaptureRecords"], 1)

    def test_out_of_range_capture_payload_rejected(self):
        data = native_blob()
        slot = capture(data)
        struct.pack_into("<I", data, slot + analyze.LAYOUT["rangesOffset"] + 16, 1048575)
        with self.assertRaises(analyze.FormatError):
            analyze.emergency(analyze.Blob(data))

    def test_future_map_claim_does_not_name_prior_fault(self):
        # The claim sequence can be visible before the producer records its QPC.
        data = native_blob()
        pack_map(data, map_record(qpc=501))
        capture(data, qpc=500)
        event = analyze.emergency(analyze.Blob(data))["events"][0]
        self.assertEqual(event["addressMappings"], [])

    def test_method_lifetime_and_later_address_reuse(self):
        old = map_record()
        freed = map_record(sequence=2, kind=3, address=0, length=0, qpc=400)
        new = map_record(sequence=3, method=18, qpc=450)
        self.assertEqual(analyze.resolve(0x1001, 2, [old, freed]), [])
        result = analyze.resolve(0x1001, 3, [old, freed, new])
        self.assertEqual([m["mapSequence"] for m in result], [3])

    def test_future_method_free_does_not_erase_prior_mapping(self):
        records = [map_record(), map_record(sequence=2, kind=3, address=0, length=0, qpc=700)]
        self.assertEqual(analyze.resolve(0x1001, 2, records, qpc=500)[0]["mapSequence"], 1)

    def test_chunk_destroy_invalidates_old_code_not_new_generation(self):
        records = [map_record(kind=5, method=0), map_record(sequence=2, qpc=350),
                   map_record(sequence=3, kind=6, length=0, method=0, qpc=400),
                   map_record(sequence=4, method=19, qpc=450)]
        self.assertEqual(analyze.resolve(0x1001, 3, records), [])
        self.assertEqual(analyze.resolve(0x1001, 4, records)[0]["mapSequence"], 4)

    def test_known_chunk_destroy_span_works_without_created_archive_record(self):
        # Generic ChunkCreated may be intentionally omitted after its 8 MiB
        # budget, while the native-tracked destruction span remains mandatory.
        records = [map_record(address=0x1010, length=32),
                   map_record(sequence=2, kind=1, method=0),
                   map_record(sequence=3, address=0x3000, length=32, method=18),
                   map_record(sequence=4, kind=6, address=0x1000, length=0x1000, method=0, qpc=400)]
        affected = analyze.resolve(0x1011, 4, records)
        self.assertEqual([m["kind"] for m in affected], ["registered-image"])
        unaffected = analyze.resolve(0x3001, 4, records)
        self.assertEqual([m["mapSequence"] for m in unaffected], [3])

    def test_unknown_chunk_span_suppresses_old_generated_names_not_new_or_images(self):
        records = [map_record(),
                   map_record(sequence=2, kind=4, address=0x3000, method=0),
                   map_record(sequence=3, kind=5, method=0),
                   map_record(sequence=4, kind=1, method=0),
                   map_record(sequence=5, kind=6, address=0x5000, length=0, method=0, qpc=400)]
        self.assertEqual([m["kind"] for m in analyze.resolve(0x1001, 5, records)], ["registered-image"])
        self.assertEqual(analyze.resolve(0x3001, 5, records), [])
        records.append(map_record(sequence=6, method=99, qpc=450))
        self.assertEqual([m["mapSequence"] for m in analyze.resolve(0x1001, 6, records)], [6, 4])

    def test_generic_buffer_is_not_named_method(self):
        result = analyze.resolve(0x1001, 1, [map_record(kind=4, method=0)])
        self.assertEqual(result[0]["kind"], "generic-code-buffer")

    def test_interval_index_handles_overlapping_images_and_many_methods(self):
        records = [map_record(kind=1, method=0, address=0x1000, length=0x100000)]
        records += [map_record(sequence=i + 2, address=0x2000 + i * 32, length=16, method=i + 1) for i in range(10000)]
        resolver = analyze.MapResolver(20000, records)
        self.assertEqual(len(resolver.resolve(0x2001)), 2)
        self.assertEqual(len(resolver.resolve(0x2011)), 1)

    def test_journal_partial_and_uncommitted_records_rejected(self):
        header = struct.pack("<8sIIQ", b"CDMAPS1\0", 1, 768, 0)
        for body in (b"x", bytes(768)):
            with self.assertRaises(analyze.FormatError):
                analyze.journal(analyze.Blob(header + body), "maps")

    def test_compact_map_journal_roundtrip_uses_exact_text_lengths(self):
        rows = [map_record(), map_record(sequence=2, qpc=301)]
        data = compact_journal(rows)
        self.assertEqual(len(data), 24 + sum(64 + len(row["name"].encode()) + len(row["image"].encode()) + len(row["sha256"].encode()) for row in rows))
        self.assertEqual(analyze.journal(analyze.Blob(data), "maps"), rows)

    def test_historical_fixed_map_journal_remains_supported(self):
        row = map_record()
        data = bytearray(24 + 768)
        struct.pack_into("<8sIIQ", data, 0, b"CDMAPS1\0", 1, 768, 0)
        struct.pack_into("<QQQQQQII", data, 24, 1, 1, row["qpc"], row["address"], row["length"], row["method"], row["kind"], 0)
        data[80:80 + len(row["name"])] = row["name"].encode()
        data[592:592 + len(row["image"])] = row["image"].encode()
        self.assertEqual(analyze.journal(analyze.Blob(data), "maps"), [row])

    def test_compact_length_and_partial_payload_rejected(self):
        valid = compact_journal([map_record()])
        excessive = bytearray(valid)
        struct.pack_into("<H", excessive, 24 + 56, 513)
        for data in (excessive, valid[:24 + 63], valid[:-1]):
            with self.assertRaises(analyze.FormatError):
                analyze.journal(analyze.Blob(data), "maps")

    def test_compact_sequence_and_qpc_order_rejected(self):
        first = map_record(qpc=400)
        for second in (map_record(sequence=1, qpc=401), map_record(sequence=2, qpc=399), map_record(sequence=2, qpc=0)):
            with self.assertRaises(analyze.FormatError):
                analyze.journal(analyze.Blob(compact_journal([first, second])), "maps")

    def test_corrupt_optional_journals_do_not_hide_emergency_fault_bytes(self):
        data = native_blob()
        pack_map(data, map_record())
        capture(data)
        with tempfile.TemporaryDirectory(prefix="cd-analyzer-") as work:
            path = pathlib.Path(work)
            (path / "emergency.bin").write_bytes(data)
            (path / "code-maps.bin").write_bytes(compact_journal([map_record()])[:-1])
            (path / "trail.bin").write_bytes(b"bad journal")
            result = analyze.analyze_file(path / "emergency.bin")
            self.assertTrue(result["events"][0]["faultCodeCaptured"])
            self.assertEqual(result["events"][0]["addressMappings"][0]["kind"], "named-jit-method")
            self.assertEqual(sum("Optional " in item and "rejected" in item for item in result["limitations"]), 2)

    def test_optional_archive_ring_contradiction_keeps_emergency_evidence(self):
        data = native_blob()
        pack_map(data, map_record())
        capture(data)
        altered = map_record()
        altered["name"] = "ContradictingOptionalArchiveName"
        with tempfile.TemporaryDirectory(prefix="cd-analyzer-") as work:
            path = pathlib.Path(work)
            (path / "emergency.bin").write_bytes(data)
            (path / "code-maps.bin").write_bytes(compact_journal([altered]))
            result = analyze.analyze_file(path / "emergency.bin")
            self.assertTrue(result["events"][0]["faultCodeCaptured"])
            self.assertEqual(result["events"][0]["addressMappings"][0]["name"], map_record()["name"])
            self.assertTrue(any("inconsistency rejected" in item for item in result["limitations"]))

    def test_conflicting_ring_archive_identity_rejected(self):
        one, two = map_record(), map_record(qpc=301)
        with self.assertRaises(analyze.FormatError):
            analyze.merged_records([one], [two])

    def test_log_does_not_assign_module_to_unnamed_frame(self):
        text = b"SHCDE-SE native crash report\nProcess ID: 2 Thread ID: 3\nException: EXCEPTION_ACCESS_VIOLATION (0xC0000005) at 0x78035D45\nAccess: read at virtual address 0x00000000\n #0 0x78035D45\n"
        event = analyze.crash_log(analyze.Blob(text))["events"][0]
        self.assertEqual(event["frames"][0]["description"], "")
        self.assertNotIn("cause", event)

    def test_metadata_mismatched_process_rejected(self):
        with tempfile.TemporaryDirectory(prefix="cd-analyzer-") as work:
            path = pathlib.Path(work)
            (path / "session.json").write_text(json.dumps({"schemaVersion": 1, "processId": 999}))
            with self.assertRaises(analyze.FormatError):
                analyze.enrich_session(path, {"processId": 123, "events": [], "limitations": []})

    def test_historical_module_and_stub_keep_uncertainty(self):
        with tempfile.TemporaryDirectory(prefix="cd-analyzer-") as work:
            path = pathlib.Path(work)
            (path / "logs").mkdir()
            (path / "session.json").write_text(json.dumps({"schemaVersion": 1, "processId": 123}))
            (path / "inventory.json").write_text(json.dumps({"schemaVersion": 1, "modules": [{"name": "x.dll", "baseAddress": "0x1000", "imageSize": 100}], "capturedUtc": "2026-01-01T00:00:00Z"}))
            (path / "logs" / "LogOutput.log").write_text('["probe"] compiled stub at 0x1000, size: 50 bytes (target: 0x2000).')
            result = {"processId": 123, "events": [{"exception": {"address": "0x1001"}, "utcFiletime": 1}], "limitations": []}
            analyze.enrich_session(path, result)
            event = result["events"][0]
            self.assertEqual(len(event["historicalAddressMappings"]), 2)
            self.assertFalse(event["historicalAddressMappings"][0]["capturedBeforeEvent"])
            self.assertNotIn("cause", event)


if __name__ == "__main__":
    unittest.main()
