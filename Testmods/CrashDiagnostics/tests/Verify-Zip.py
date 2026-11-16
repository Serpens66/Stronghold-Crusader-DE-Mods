"""Validate exported production bytes/CRC, independent of the collector's writer."""
import sys
import zipfile

with zipfile.ZipFile(sys.argv[1]) as archive:
    assert archive.testzip() is None, "ZIP CRC mismatch"
    assert "emergency.bin" in archive.namelist(), "Missing native evidence"
    assert sys.argv[2] == "collectorloss" or "report.txt" in archive.namelist(), "Missing report"
    assert sum(entry.file_size for entry in archive.infolist()) <= 128 * 1024 * 1024
