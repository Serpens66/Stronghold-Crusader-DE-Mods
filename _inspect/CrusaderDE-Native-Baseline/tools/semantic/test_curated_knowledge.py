#!/usr/bin/env python3
"""Regression tests for the curated semantic-knowledge contract."""

from __future__ import annotations

import copy
import importlib.util
import json
import unittest
from pathlib import Path


TOOL_DIR = Path(__file__).resolve().parent
BASELINE = TOOL_DIR.parent.parent
WORKSPACE = BASELINE.parent.parent
SEMANTIC = BASELINE / "sem" / "FBCB9319"
COMPARISON = BASELINE / "diff" / "17F8DD4A-FBCB9319"
NATIVE = Path(r"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll")
CURRENT_HASH = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2"

SPEC = importlib.util.spec_from_file_location("curated_knowledge", TOOL_DIR / "curated_knowledge.py")
CURATED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CURATED)


class CuratedKnowledgeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.claims = CURATED.read_jsonl(SEMANTIC / "knowledge" / "function-claims.jsonl")
        cls.spans = CURATED.read_jsonl(SEMANTIC / "knowledge" / "hook-spans.jsonl")
        cls.contracts = CURATED.read_jsonl(SEMANTIC / "knowledge" / "api-contracts.jsonl")
        cls.functions = CURATED.read_jsonl(SEMANTIC / "exports" / "semantic-functions.jsonl")
        cls.matches = CURATED.read_jsonl(COMPARISON / "version-matches.jsonl")
        cls.image, cls.image_base = CURATED.load_pe_image(NATIVE)

    def test_full_curated_dataset_validates(self):
        labels = CURATED.validate_claims(
            self.claims, self.functions, CURRENT_HASH, self.image_base, WORKSPACE)
        CURATED.validate_hook_spans(self.spans, self.claims, CURRENT_HASH, self.image)
        CURATED.validate_contracts(self.contracts, CURRENT_HASH)
        self.assertEqual(3, len(labels))

    def test_hook_byte_mutation_fails_closed(self):
        spans = copy.deepcopy(self.spans)
        spans[0]["originalBytes"] = "00" + spans[0]["originalBytes"][2:]
        with self.assertRaisesRegex(ValueError, "Original bytes mismatch"):
            CURATED.validate_hook_spans(spans, self.claims, CURRENT_HASH, self.image)

    def test_probable_claim_requires_two_independent_chains(self):
        claim = copy.deepcopy(next(row for row in self.claims if row["semanticConfidence"] == "probable"))
        claim["evidence"] = claim["evidence"][:1]
        with self.assertRaisesRegex(ValueError, "Probable claim lacks two chains"):
            CURATED.validate_evidence(claim, WORKSPACE)

    def test_gatehouse_unit_id_bug_is_not_an_active_contract(self):
        active = [row for row in self.contracts
                  if row.get("contractId") == "gatehouse-query-unit-id" and row.get("status") == "active"]
        self.assertEqual([], active)

    def test_reports_preserve_version_semantic_gap(self):
        report = CURATED.build_reports(self.claims, self.spans, self.contracts, self.matches, self.functions)
        self.assertTrue(any(row["claimId"] == "fn-unit-update-monk" for row in report["confidenceGaps"]))
        self.assertTrue(all(row["compatible"] for row in report["callerAbi"]))


if __name__ == "__main__":
    unittest.main()
