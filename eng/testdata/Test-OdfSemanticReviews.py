"""Mutation regressions for semantic evidence coverage and false waiver claims."""

import copy
import importlib.util
import sys
import unittest
from pathlib import Path

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("semantic", Path(__file__).resolve().parents[1] / "Test-OdfSemanticReviews.py")
semantic = importlib.util.module_from_spec(spec)
spec.loader.exec_module(semantic)


class SemanticContracts(unittest.TestCase):
    def setUp(self):
        self.groups = {"g": {"requiresFollowUp": True, "memberSha256": "locked", "versions": ["1.4"]},
                       "unreviewed": {"requiresFollowUp": True}}
        self.members = {"g": {"a", "b"}}
        rule = {"kind": "requirement", "sdkResponsibilities": ["validator"], "assessment": "Validate QName",
                "conditions": "When present", "exemption": "none", "units": ["a", "b"]}
        self.document = {"schemaVersion": 1, "fullConformanceClaim": False, "reviews": [{"groupId": "g",
            "memberSha256": "locked", "versions": ["1.4"], "versionAssessment": "Only 1.4 reviewed",
            "implementationAdjudication": "pending", "rules": [rule]}]}

    def test_review_does_not_close_other_groups_or_implementation(self):
        result = semantic.validate(self.document, self.groups, self.members)
        self.assertEqual(1, result["semanticPendingGroups"])
        self.assertEqual(2, result["implementationPendingGroups"])
        self.assertFalse(result["semanticClassificationComplete"])

    def test_changed_group_missing_foreign_or_duplicate_member_fails(self):
        for units in (["a"], ["a", "foreign"], ["a", "b", "b"]):
            document = copy.deepcopy(self.document)
            document["reviews"][0]["rules"][0]["units"] = units
            with self.assertRaises(ValueError):
                semantic.validate(document, self.groups, self.members)
        self.document["reviews"][0]["memberSha256"] = "stale"
        with self.assertRaisesRegex(ValueError, "membership changed"):
            semantic.validate(self.document, self.groups, self.members)

    def test_normative_rule_cannot_be_waived_as_context(self):
        rule = self.document["reviews"][0]["rules"][0]
        rule.update(exemption="non-obligation", sdkResponsibilities=["source-context"])
        with self.assertRaisesRegex(ValueError, "source context"):
            semantic.validate(self.document, self.groups, self.members)

    def test_normative_rule_cannot_hide_sdk_responsibility_without_explicit_waiver(self):
        self.document["reviews"][0]["rules"][0]["sdkResponsibilities"] = ["source-context"]
        with self.assertRaisesRegex(ValueError, "hidden in source context"):
            semantic.validate(self.document, self.groups, self.members)

    def test_sdk_exclusion_needs_evidence_and_cannot_escape_repository(self):
        rule = self.document["reviews"][0]["rules"][0]
        rule.update(exemption="outside-sdk", sdkResponsibilities=["outside-sdk"])
        with self.assertRaisesRegex(ValueError, "scope evidence"):
            semantic.validate(self.document, self.groups, self.members)
        rule["scopeEvidence"] = ["../outside.md"]
        with self.assertRaisesRegex(ValueError, "unsafe scope evidence"):
            semantic.validate(self.document, self.groups, self.members)

    def test_classification_cannot_claim_implementation_complete(self):
        self.document["reviews"][0]["implementationAdjudication"] = "fulfilled"
        with self.assertRaisesRegex(ValueError, "implementation adjudication"):
            semantic.validate(self.document, self.groups, self.members)

    def test_duplicate_groups_and_unassessed_versions_fail(self):
        document = copy.deepcopy(self.document)
        document["reviews"] *= 2
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            semantic.validate(document, self.groups, self.members)
        self.document["reviews"][0]["versions"] = ["1.3", "1.4"]
        with self.assertRaisesRegex(ValueError, "version assessment"):
            semantic.validate(self.document, self.groups, self.members)


if __name__ == "__main__":
    unittest.main()
