"""Regression tests for source queues that must never imply SDK exemptions."""

import importlib.util
import json
import sys
import unittest
from pathlib import Path

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("classification", Path(__file__).resolve().parents[1] / "Build-OdfObligationClassification.py")
classification = importlib.util.module_from_spec(spec)
spec.loader.exec_module(classification)


def source(**changes):
    row = {"id": "ODF-1.4/schema/B00001/S001", "version": "1.4", "part": "schema",
           "sentenceSha256": "original", "blockSha256": "block", "status": "evidence-gap",
           "reviewState": "unreviewed", "scope": "potential-sdk-obligation", "decision": "NO-SENTENCE-PROOF",
           "locator": "/office:text/p[1]"}
    row.update(changes)
    return row


class SourceClassificationTests(unittest.TestCase):
    def test_every_work_queue_has_a_user_facing_label(self):
        self.assertFalse(classification.QUEUES - classification.LABELS.keys())

    def test_source_chapter_does_not_exempt_display_or_formula_sdk_behavior(self):
        for chapter, queue in (("Graphic Content", "graphics-and-presentation-content"),
                               ("Formula Processing Model", "formula-processing-and-types"),
                               ("Database Front-end Document Content", "database-document-content")):
            result = classification.classify(source(), {"headingPath": [chapter], "kind": "paragraph"})
            self.assertEqual(queue, result["queue"])
            self.assertEqual("undetermined", result["sdkScope"])
            self.assertEqual("pending", result["semanticClassification"])

    def test_explicit_informative_heading_records_context_without_waiving_units(self):
        row = source()
        result = classification.classify(row, {"headingPath": ["Changes (Non Normative)"], "kind": "paragraph"})
        self.assertEqual("explicit-informative-heading", result["sourceContext"])
        self.assertEqual("undetermined", result["sdkScope"])
        classification.validate_partition([row], [result])
        self.assertEqual("evidence-gap", row["status"])

    def test_pdf_errata_and_objects_remain_executable_review_queues(self):
        for row, queue in ((source(version="1.0", part="spec"), "pdf-source-reconstruction"),
                           (source(part="errata02"), "effective-version-and-errata-reconciliation"),
                           (source(reviewState="unreviewed-object", sentenceSha256=""), "opaque-object-content-review")):
            result = classification.classify(row, {})
            self.assertEqual(queue, result["queue"])
            self.assertEqual(row["sentenceSha256"] or row["blockSha256"], result["sourceUnitSha256"])

    def test_duplicate_missing_changed_or_automatically_waived_units_are_rejected(self):
        row = source()
        result = classification.classify(row, {})
        with self.assertRaisesRegex(ValueError, "every source unit"):
            classification.validate_partition([row], [])
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            classification.validate_partition([row, source(id="another")], [result, result])
        with self.assertRaisesRegex(ValueError, "source changed"):
            classification.validate_partition([row], [{**result, "sourceUnitSha256": "changed"}])
        with self.assertRaisesRegex(ValueError, "semantic verdict"):
            classification.validate_partition([row], [{**result, "sdkScope": "out-of-sdk-scope"}])
        with self.assertRaisesRegex(ValueError, "Missing classification"):
            classification.validate_partition([row], [{**result, "queue": "arbitrary-label"}])

    def test_exact_existing_scope_decisions_are_preserved(self):
        row = source(reviewState="reviewed", scope="package-validator", decision="SENT-PKG-001")
        result = classification.classify(row, {})
        classification.validate_partition([row], [result])
        self.assertEqual("package-validator", result["sdkScope"])
        with self.assertRaisesRegex(ValueError, "changed an existing"):
            classification.validate_partition([row], [{**result, "sdkScope": "out-of-sdk-scope"}])

    def test_cross_version_groups_are_candidates_and_keep_different_contexts_separate(self):
        context = {"headingPath": ["General Attributes", "draw:engine"], "kind": "paragraph"}
        older = classification.classify(source(version="1.2"), context)
        newer = classification.classify(source(version="1.4"), context)
        self.assertEqual(older["groupId"], newer["groupId"])
        other = classification.classify(source(), {**context, "headingPath": ["General Attributes", "draw:name"]})
        self.assertNotEqual(newer["groupId"], other["groupId"])
        self.assertEqual("pending", newer["semanticClassification"])
        self.assertEqual("undetermined", newer["sdkScope"])

    def test_pdf_reconstruction_batches_do_not_merge_unrelated_physical_pages(self):
        first = classification.classify(source(version="1.0", part="spec", locator="page=31"), {})
        second = classification.classify(source(version="1.0", part="spec", locator="page=32"), {})
        self.assertNotEqual(first["groupId"], second["groupId"])

    def test_group_identity_tampering_is_rejected(self):
        row = source()
        result = classification.classify(row, {})
        with self.assertRaisesRegex(ValueError, "Grouping identity changed"):
            classification.validate_partition([row], [{**result, "groupId": "other"}])

    def test_navigation_group_does_not_claim_existing_semantic_decisions(self):
        row = source(status="navigation", scope="publication", decision="SOURCE-CONTEXT")
        result = classification.classify(row, {"headingPath": ["Tables"], "kind": "heading"})
        files, summary = classification.outputs([row], [result], [])
        document = next(value for path, value in files.items() if path.name == "groups.json")
        self.assertEqual("source-context-only", json.loads(document)["groups"][0]["groupReviewState"])
        self.assertFalse(summary["crossVersionEquivalenceEstablished"])

    def test_reviewed_partial_group_is_still_counted_as_remaining_work(self):
        row = source(status="partial", reviewState="reviewed", scope="validator", decision="SENT-TEST-001")
        result = classification.classify(row, {"headingPath": ["Datatypes"], "kind": "paragraph"})
        _, summary = classification.outputs([row], [result], [])
        self.assertEqual(0, summary["unresolvedGroupCount"])
        self.assertEqual(1, summary["reviewedPartialOnlyGroupCount"])
        self.assertEqual(1, summary["needsFollowUpGroupCount"])


if __name__ == "__main__":
    unittest.main()
