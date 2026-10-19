"""Mutation regressions for semantic evidence coverage and false waiver claims."""

import copy
import importlib.util
import json
import sys
import tempfile
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

    def test_editorial_projection_requires_exact_reviewed_evidence(self):
        block = "ODF-1.1/spec/B00001"
        self.document["reviews"][0]["rules"][0]["units"] = [block + "/S001"]
        records = {block: {"effectiveBlockSha256": "effective", "deletionRuns": [{"textSha256": "deleted"}]}}
        with self.assertRaisesRegex(ValueError, "effective source text changed"):
            semantic.validate_effective_reviews(self.document, records)
        self.document["reviews"][0]["effectiveTextEvidenceSha256"] = semantic.object_review_hash(records, {block})
        self.assertEqual(1, semantic.validate_effective_reviews(self.document, records))
        records[block]["deletionRuns"][0]["textSha256"] = "different"
        with self.assertRaisesRegex(ValueError, "effective source text changed"):
            semantic.validate_effective_reviews(self.document, records)

    def test_effective_evidence_cannot_be_reused_for_another_block(self):
        self.document["reviews"][0]["effectiveTextEvidenceSha256"] = "unrelated"
        with self.assertRaisesRegex(ValueError, "effective source text changed"):
            semantic.validate_effective_reviews(self.document, {})

    def test_pdf_grammar_review_is_bound_to_exact_pages_and_projection(self):
        block = "ODF-1.0/spec/B00023"
        self.document["reviews"][0]["rules"][0]["units"] = [block + "/S001"]
        self.document["reviews"][0].update(pdfGrammarPages=[53], sourcePdfGrammarEvidenceSha256="locked")
        evidence = {"pages": [{"page": 53, "sourceBlockId": block}]}
        self.assertEqual(1, semantic.validate_pdf_grammar_reviews(self.document, evidence, "locked"))
        with self.assertRaisesRegex(ValueError, "changed reviewed PDF"):
            semantic.validate_pdf_grammar_reviews(self.document, evidence, "changed")
        self.document["reviews"][0]["rules"][0]["units"] = ["ODF-1.0/spec/B00024/S001"]
        with self.assertRaisesRegex(ValueError, "different source pages"):
            semantic.validate_pdf_grammar_reviews(self.document, evidence, "locked")

    def test_pdf_grammar_review_cannot_reuse_unknown_or_duplicate_pages(self):
        evidence = {"pages": [{"page": 53, "sourceBlockId": "ODF-1.0/spec/B00023"}]}
        for pages in ([], [53, 53], [54], [True]):
            self.document["reviews"][0].update(pdfGrammarPages=pages, sourcePdfGrammarEvidenceSha256="locked")
            with self.assertRaises(ValueError):
                semantic.validate_pdf_grammar_reviews(self.document, evidence, "locked")

    def test_review_does_not_close_other_groups_or_implementation(self):
        result = semantic.validate(self.document, self.groups, self.members)
        self.assertEqual(1, result["semanticPendingGroups"])
        self.assertEqual(2, result["implementationPendingGroups"])
        self.assertFalse(result["semanticClassificationComplete"])

    def test_closed_group_keeps_review_history_without_reducing_other_backlog(self):
        self.groups["g"]["requiresFollowUp"] = False
        result = semantic.validate(self.document, self.groups, self.members)
        self.assertEqual(1, result["semanticReviewedGroups"])
        self.assertEqual(1, result["semanticPendingGroups"])
        self.assertEqual(1, result["implementationPendingGroups"])
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

    def test_informative_waiver_requires_units_inside_explicit_appendix(self):
        scratch = Path(__file__).resolve().parents[2] / "artifacts/odf-regression"
        scratch.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=scratch) as directory:
            root = Path(directory)
            region = {"first": {"id": "ODF-1.4/schema/B00010"}, "afterLast": {"id": "ODF-1.4/schema/B00020"}}
            (root / "bounds.json").write_text(json.dumps({"informativeContexts": [region]}), encoding="utf-8")
            review = self.document["reviews"][0]
            review["sourceContextEvidence"] = "bounds.json"
            review["sourceContextEvidenceSha256"] = semantic.hashlib.sha256((root / "bounds.json").read_bytes()).hexdigest()
            rule = review["rules"][0]
            rule.update(kind="informative-context", sdkResponsibilities=["source-context"], exemption="non-obligation")
            for block, accepted in (("ODF-1.4/schema/B00010", True), ("ODF-1.4/schema/B00019", True),
                                    ("ODF-1.4/schema/B00020", False), ("ODF-1.3/schema/B00015", False)):
                unit = block + "/S001"
                rule["units"] = [unit]
                self.members["g"] = {unit}
                if accepted:
                    semantic.validate(self.document, self.groups, self.members, root)
                else:
                    with self.assertRaisesRegex(ValueError, "outside its source appendix"):
                        semantic.validate(self.document, self.groups, self.members, root)

    def test_object_context_requires_explicit_membership_in_same_publication(self):
        scratch = Path(__file__).resolve().parents[2] / "artifacts/odf-regression"
        with tempfile.TemporaryDirectory(dir=scratch) as directory:
            root = Path(directory)
            region = {"first": {"id": "ODF-1.4/schema/B00010"}, "afterLast": {"id": "ODF-1.4/schema/B00020"},
                      "objectUnitIds": ["ODF-1.4/schema/O00001", "ODF-1.3/schema/O00001"]}
            policy = root / "bounds.json"
            policy.write_text(json.dumps({"informativeContexts": [region]}), encoding="utf-8")
            review = self.document["reviews"][0]
            review.update(sourceContextEvidence="bounds.json", sourceContextEvidenceSha256=semantic.hashlib.sha256(policy.read_bytes()).hexdigest())
            rule = review["rules"][0]
            rule.update(kind="informative-context", sdkResponsibilities=["source-context"], exemption="non-obligation")
            for unit, accepted in (("ODF-1.4/schema/O00001", True), ("ODF-1.4/schema/O00002", False), ("ODF-1.3/schema/O00001", False)):
                rule["units"] = [unit]
                self.members["g"] = {unit}
                if accepted:
                    semantic.validate(self.document, self.groups, self.members, root)
                else:
                    with self.assertRaisesRegex(ValueError, "outside its source appendix"):
                        semantic.validate(self.document, self.groups, self.members, root)
            policy.write_text(json.dumps({"informativeContexts": []}), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "changed after review"):
                semantic.validate(self.document, self.groups, self.members, root)

    def context_marker_fixture(self):
        regions = {"first": {"id": "ODF-1.4/schema/B00010", "title": "Note: Explanation.", "kind": "paragraph"},
                   "afterLast": {"id": "ODF-1.4/schema/B00011", "title": "Normative text.", "kind": "paragraph"},
                   "sourceArchiveSha256": "locked"}
        rows = {}
        for marker in regions.values():
            if not isinstance(marker, dict):
                continue
            marker["textSha256"] = semantic.hashlib.sha256(marker["title"].encode()).hexdigest()
            rows[marker["id"] + "/S001"] = {"blockSha256": marker["textSha256"], "locator": "/office:text/p[1]"}
        sources = [{"version": "1.4", "part": "schema", "sha256": "locked"}]
        return regions, rows, sources

    def check_context_markers(self, root, region, rows, sources):
        policy = root / "bounds.json"
        policy.write_text(json.dumps({"informativeContexts": [region]}), encoding="utf-8")
        review = self.document["reviews"][0]
        review.update(sourceContextEvidence="bounds.json",
                      sourceContextEvidenceSha256=semantic.hashlib.sha256(policy.read_bytes()).hexdigest())
        return semantic.validate_source_context_markers(self.document, rows, sources, root)

    def test_context_markers_bind_source_text_after_policy_rehash(self):
        scratch = Path(__file__).resolve().parents[2] / "artifacts/odf-regression"
        with tempfile.TemporaryDirectory(dir=scratch) as directory:
            root = Path(directory)
            region, rows, sources = self.context_marker_fixture()
            self.assertEqual(1, self.check_context_markers(root, region, rows, sources))
            for key in ("title", "textSha256", "id"):
                changed = copy.deepcopy(region)
                changed["first"][key] = "changed"
                with self.assertRaisesRegex(ValueError, "marker text|bounds"):
                    self.check_context_markers(root, changed, rows, sources)

    def test_context_markers_reject_wrong_archive_kind_and_location(self):
        scratch = Path(__file__).resolve().parents[2] / "artifacts/odf-regression"
        with tempfile.TemporaryDirectory(dir=scratch) as directory:
            root = Path(directory)
            region, rows, sources = self.context_marker_fixture()
            for key, value in (("archive", "different"), ("kind", "heading"), ("locator", "/wrong/p[1]")):
                changed = copy.deepcopy(region)
                if key == "archive":
                    changed["sourceArchiveSha256"] = value
                else:
                    changed["first"][key] = value
                with self.assertRaisesRegex(ValueError, "archive|kind or location"):
                    self.check_context_markers(root, changed, rows, sources)
            del region["sourceArchiveSha256"]
            self.document["reviews"][0]["sourceArchiveSha256"] = {"ODF-1.4/schema": "locked"}
            self.assertEqual(1, self.check_context_markers(root, region, rows, sources))

    def test_context_markers_cannot_cross_publications_or_reverse_bounds(self):
        scratch = Path(__file__).resolve().parents[2] / "artifacts/odf-regression"
        with tempfile.TemporaryDirectory(dir=scratch) as directory:
            root = Path(directory)
            region, rows, sources = self.context_marker_fixture()
            for after in ("ODF-1.3/schema/B00011", "ODF-1.4/packages/B00011", "ODF-1.4/schema/B00009"):
                changed = copy.deepcopy(region)
                changed["afterLast"]["id"] = after
                with self.assertRaisesRegex(ValueError, "bounds cross publications or are reversed"):
                    self.check_context_markers(root, changed, rows, sources)

    def test_context_markers_can_bind_hashes_without_repeating_publication_text(self):
        scratch = Path(__file__).resolve().parents[2] / "artifacts/odf-regression"
        with tempfile.TemporaryDirectory(dir=scratch) as directory:
            root = Path(directory)
            region, rows, sources = self.context_marker_fixture()
            for key in ("first", "afterLast"):
                del region[key]["title"]
            self.assertEqual(1, self.check_context_markers(root, region, rows, sources))
            region["first"]["textSha256"] = "different"
            with self.assertRaisesRegex(ValueError, "marker text differs"):
                self.check_context_markers(root, region, rows, sources)

    def test_note_context_cannot_extend_into_the_next_paragraph(self):
        scratch = Path(__file__).resolve().parents[2] / "artifacts/odf-regression"
        with tempfile.TemporaryDirectory(dir=scratch) as directory:
            root = Path(directory)
            region, rows, sources = self.context_marker_fixture()
            region["first"]["label"] = "Note:"
            self.assertEqual(1, self.check_context_markers(root, region, rows, sources))
            changed = copy.deepcopy(region)
            changed["afterLast"]["id"] = "ODF-1.4/schema/B00012"
            with self.assertRaisesRegex(ValueError, "Note context must end at the next source block"):
                self.check_context_markers(root, changed, rows, sources)
            region["first"]["label"] = "Arbitrary prose"
            with self.assertRaisesRegex(ValueError, "Unknown source context label"):
                self.check_context_markers(root, region, rows, sources)

    def test_numbered_notes_preserve_source_hashes_and_boundaries(self):
        scratch = Path(__file__).resolve().parents[2] / "artifacts/odf-regression"
        with tempfile.TemporaryDirectory(dir=scratch) as directory:
            root = Path(directory)
            for label in ("Note 1:", "Note 2:"):
                region, rows, sources = self.context_marker_fixture()
                marker = region["first"]
                marker.update(label=label, title=label + " Explanation.")
                marker["textSha256"] = semantic.hashlib.sha256(marker["title"].encode()).hexdigest()
                rows[marker["id"] + "/S001"]["blockSha256"] = marker["textSha256"]
                self.assertEqual(1, self.check_context_markers(root, region, rows, sources))
                marker["textSha256"] = "changed"
                with self.assertRaisesRegex(ValueError, "marker text differs"):
                    self.check_context_markers(root, region, rows, sources)

    def test_numbered_note_cannot_include_the_next_paragraph(self):
        scratch = Path(__file__).resolve().parents[2] / "artifacts/odf-regression"
        with tempfile.TemporaryDirectory(dir=scratch) as directory:
            root = Path(directory)
            region, rows, sources = self.context_marker_fixture()
            del region["first"]["title"]
            region["first"]["label"] = "Note 1:"
            region["afterLast"]["id"] = "ODF-1.4/schema/B00012"
            with self.assertRaisesRegex(ValueError, "Note context must end at the next source block"):
                self.check_context_markers(root, region, rows, sources)

    def test_note_label_requires_canonical_positive_number(self):
        scratch = Path(__file__).resolve().parents[2] / "artifacts/odf-regression"
        with tempfile.TemporaryDirectory(dir=scratch) as directory:
            root = Path(directory)
            for label in ("Note 0:", "Note 01:", "Note -1:", "Note 1.1:", "Note1:", "Note 1", 1):
                region, rows, sources = self.context_marker_fixture()
                region["first"]["label"] = label
                with self.assertRaisesRegex(ValueError, "Unknown source context label"):
                    self.check_context_markers(root, region, rows, sources)

    def test_note_label_must_match_available_source_title(self):
        scratch = Path(__file__).resolve().parents[2] / "artifacts/odf-regression"
        with tempfile.TemporaryDirectory(dir=scratch) as directory:
            root = Path(directory)
            region, rows, sources = self.context_marker_fixture()
            region["first"]["label"] = "Note 1:"
            with self.assertRaisesRegex(ValueError, "label does not match"):
                self.check_context_markers(root, region, rows, sources)

    def test_object_review_is_bound_to_payload_not_only_xml_reference(self):
        unit = "ODF-1.4/formula/O00001"
        self.document["reviews"][0]["rules"][0]["units"] = [unit]
        records = {unit: {"sourceObjectSha256": "unchanged-reference", "payload": {"sha256": "first-payload"}}}
        review = self.document["reviews"][0]
        review["objectEvidenceSha256"] = semantic.object_review_hash(records, {unit})
        self.assertEqual(1, semantic.validate_object_reviews(self.document, records))
        records[unit]["payload"]["sha256"] = "different-payload"
        with self.assertRaisesRegex(ValueError, "payload, archive"):
            semantic.validate_object_reviews(self.document, records)

    def test_unknown_object_cannot_be_reviewed_without_reconstruction(self):
        self.document["reviews"][0]["rules"][0]["units"] = ["ODF-1.4/formula/O00001"]
        with self.assertRaisesRegex(ValueError, "lacks reconstructed"):
            semantic.validate_object_reviews(self.document, {})

    def test_superscript_review_binds_style_even_if_plain_text_is_unchanged(self):
        block = "ODF-1.4/formula/B00001"
        review = self.document["reviews"][0]
        review["rules"][0]["units"] = [block + "/S001"]
        records = {block: {"plainTextSha256": "same-239", "positionedText": [{"text": "39", "position": "super 58%"}]}}
        review["sourceInlineEvidenceSha256"] = semantic.object_review_hash(records, {block})
        self.assertEqual(1, semantic.validate_inline_reviews(self.document, records))
        records[block]["positionedText"][0]["position"] = "sub 58%"
        with self.assertRaisesRegex(ValueError, "superscript"):
            semantic.validate_inline_reviews(self.document, records)

    def test_positioned_formula_requires_independent_review_binding(self):
        block = "ODF-1.4/formula/B00001"
        self.document["reviews"][0]["rules"][0]["units"] = [block + "/S001"]
        with self.assertRaisesRegex(ValueError, "inline typography"):
            semantic.validate_inline_reviews(self.document, {block: {"positionedText": ["39"]}})

    def table_fixture(self):
        unit = "ODF-1.4/formula/B00001/S001"
        locator = "/office:text/table[1]/table-row[2]/table-cell[2]/p[1]"
        rule = self.document["reviews"][0]["rules"][0]
        rule["units"] = [unit]
        rule["tableCells"] = [{"unitId": unit, "locator": locator, "sourceUnitSha256": "locked"}]
        return {unit: {"locator": locator, "sentenceSha256": "locked"}}

    def test_table_mapping_preserves_location_not_only_text(self):
        rows = self.table_fixture()
        self.assertEqual(1, semantic.validate_table_reviews(self.document, rows))
        cell = self.document["reviews"][0]["rules"][0]["tableCells"][0]
        cell["locator"] = cell["locator"].replace("table-cell[2]", "table-cell[1]")
        with self.assertRaisesRegex(ValueError, "location or source"):
            semantic.validate_table_reviews(self.document, rows)

    def test_table_mapping_cannot_omit_a_semantic_rule_member(self):
        rows = self.table_fixture()
        self.document["reviews"][0]["rules"][0]["units"].append("another-member")
        with self.assertRaisesRegex(ValueError, "Incomplete"):
            semantic.validate_table_reviews(self.document, rows)

    def test_table_mapping_cannot_mix_different_rows(self):
        rows = self.table_fixture()
        rule = self.document["reviews"][0]["rules"][0]
        unit = "ODF-1.4/formula/B00002/S001"
        locator = "/office:text/table[1]/table-row[3]/table-cell[2]/p[1]"
        rows[unit] = {"locator": locator, "sentenceSha256": "other"}
        rule["units"].append(unit)
        rule["tableCells"].append({"unitId": unit, "locator": locator, "sourceUnitSha256": "other"})
        with self.assertRaisesRegex(ValueError, "mixed"):
            semantic.validate_table_reviews(self.document, rows)

    def test_header_table_row_keeps_its_actual_container(self):
        rows = self.table_fixture()
        cell = self.document["reviews"][0]["rules"][0]["tableCells"][0]
        cell["locator"] = cell["locator"].replace("/table-row[", "/table-header-rows[1]/table-row[")
        rows[cell["unitId"]]["locator"] = cell["locator"]
        self.assertEqual(1, semantic.validate_table_reviews(self.document, rows))

    def test_same_row_number_cannot_mix_header_and_body_across_versions(self):
        rows = self.table_fixture()
        rule = self.document["reviews"][0]["rules"][0]
        unit = "ODF-1.3/formula/B00002/S001"
        locator = "/office:text/table[1]/table-header-rows[1]/table-row[2]/table-cell[2]/p[1]"
        rows[unit] = {"locator": locator, "sentenceSha256": "other"}
        rule["units"].append(unit)
        rule["tableCells"].append({"unitId": unit, "locator": locator, "sourceUnitSha256": "other"})
        with self.assertRaisesRegex(ValueError, "mixed"):
            semantic.validate_table_reviews(self.document, rows)

    def test_scope_exemption_is_bound_to_actual_product_policy(self):
        scratch = Path(__file__).resolve().parents[2] / "artifacts/odf-regression"
        scratch.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=scratch) as directory:
            root = Path(directory)
            evidence = root / "scope.txt"
            evidence.write_text("Physical pagination uses an external renderer.\n", encoding="utf-8")
            rule = self.document["reviews"][0]["rules"][0]
            rule.update(exemption="outside-sdk", sdkResponsibilities=["outside-sdk"], scopeEvidence=["scope.txt"],
                        scopeEvidenceSha256={"scope.txt": semantic.hashlib.sha256(evidence.read_bytes().replace(b"\r\n", b"\n")).hexdigest()})
            semantic.validate_scope_evidence(self.document, root)
            evidence.write_text("Physical pagination is provided by the SDK.\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "changed after exemption"):
                semantic.validate_scope_evidence(self.document, root)

    def test_source_archive_change_invalidates_review_even_if_text_is_unchanged(self):
        review = self.document["reviews"][0]
        review["rules"][0]["units"] = ["ODF-1.1/spec/B00001/S001"]
        review["sourceArchiveSha256"] = {"ODF-1.1/spec": "original-styles"}
        sources = [{"version": "1.1", "part": "spec", "sha256": "original-styles"}]
        semantic.validate_source_archives(self.document, sources)
        sources[0]["sha256"] = "same-text-different-bold-style"
        with self.assertRaisesRegex(ValueError, "including styles"):
            semantic.validate_source_archives(self.document, sources)

    def test_empty_reference_review_cannot_ignore_changed_hidden_target(self):
        block = "ODF-1.1/spec/B00001"
        review = self.document["reviews"][0]
        review["rules"][0]["units"] = [block + "/S001"]
        records = [{"sourceBlockId": block, "cachedText": "", "name": "Image Data", "target": "correct"}]
        review["sourceReferenceEvidenceSha256"] = semantic.object_review_hash(semantic.reference_blocks(records), {block})
        self.assertEqual(1, semantic.validate_reference_reviews(self.document, records))
        records[0]["target"] = "a-different-clause"
        with self.assertRaisesRegex(ValueError, "hidden reference"):
            semantic.validate_reference_reviews(self.document, records)

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
