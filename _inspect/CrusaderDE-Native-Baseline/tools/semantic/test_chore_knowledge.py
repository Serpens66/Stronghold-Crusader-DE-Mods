#!/usr/bin/env python3
import copy
import json
import unittest
from pathlib import Path

from chore_knowledge import effective_claim


ROOT = Path(__file__).resolve().parents[4]
KNOWLEDGE = ROOT / "_inspect" / "CrusaderDE-Native-Baseline" / "sem" / "FBCB9319" / "knowledge"
OPCODES = [json.loads(line) for line in (KNOWLEDGE / "chore-opcodes.jsonl").read_text(encoding="utf-8").splitlines() if line]


class ChoreKnowledgeTests(unittest.TestCase):
    def opcode(self, value):
        return next((row for row in OPCODES if row["opcode"] == value), None)

    def test_sync_event_120(self):
        row = self.opcode(120)
        self.assertEqual("confirmed-runtime", row["status"])
        self.assertIn("runtime-peer-a", row["evidenceIds"])

    def test_probable_109(self):
        self.assertEqual("probable", self.opcode(109)["status"])

    def test_111_is_not_vanilla(self):
        row = self.opcode(111)
        self.assertNotEqual("vanilla", row["classification"])
        self.assertIn(row["status"], {"candidate", "retired"})

    def test_unknown_opcode(self):
        self.assertIsNone(self.opcode(250))

    def test_counter_evidence_changes_only_one_field(self):
        original = copy.deepcopy(self.opcode(17))
        corrected = copy.deepcopy(original)
        corrected["counterEvidence"] = [{"status": "accepted", "path": "payloadVariants.0.fields.0.name", "replacement": "correctedField"}]
        effective = effective_claim(corrected)
        self.assertEqual("correctedField", effective["payloadVariants"][0]["fields"][0]["name"])
        self.assertEqual(original["payloadVariants"][0]["size"], effective["payloadVariants"][0]["size"])
        self.assertEqual(original["confidence"]["handler"], effective["confidence"]["handler"])


if __name__ == "__main__":
    unittest.main()
