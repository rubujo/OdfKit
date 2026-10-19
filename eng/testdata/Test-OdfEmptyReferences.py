"""Keep hidden named targets, peer fields, and unresolved ambiguity explicit."""

import copy
import importlib.util
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("empty_refs", Path(__file__).resolve().parents[1] / "Build-OdfEmptyReferences.py")
refs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(refs)


class EmptyReferenceTests(unittest.TestCase):
    def project(self, content, changed=None):
        body = ET.fromstring(f'<office:text xmlns:office="{refs.OFFICE[1:-1]}" xmlns:text="{refs.TEXT[1:-1]}">{content}</office:text>')
        prefix = "/office:document-content/office:body/office:text"
        blocks = []
        counts = {}
        for node in body:
            local = node.tag.rsplit("}", 1)[-1]
            counts[local] = counts.get(local, 0) + 1
            if node.tag in (refs.TEXT + "p", refs.TEXT + "h"):
                blocks.append({"locator": f"{prefix}/{local}[{counts[local]}]", "kind": "heading" if local == "h" else "paragraph",
                               "text": "".join(node.itertext())})
        return refs.project(body, blocks, "ODF-1.1/spec", changed), blocks

    def fixture(self):
        content = '<text:p>See section <text:reference-ref text:ref-name="Target" text:reference-format="chapter"/>.</text:p><text:h><text:reference-mark text:name="Target"/>Image Data</text:h>'
        (count, records), blocks = self.project(content)
        document = {"schemaVersion": 1, "fullConformanceClaim": False, "semanticClassificationComplete": False,
                    "renderedChapterNumbers": "not-computed", "nonemptyReferenceConsistency": "pending",
                    "referenceFieldCount": count, "emptyReferenceFieldCount": 1, "references": records}
        source_blocks = {f"ODF-1.1/spec/B{index:05d}": {"blockSha256": refs.sha(block["text"].encode()), "locator": block["locator"]}
                         for index, block in enumerate(blocks, 1)}
        return document, source_blocks

    def test_empty_chapter_display_can_still_have_a_unique_target(self):
        document, blocks = self.fixture()
        record = refs.validate(document, blocks)[0]
        self.assertTrue(record["cachedTextEmpty"])
        self.assertEqual("unique-target-addressed", record["resolution"])
        self.assertEqual("ODF-1.1/spec/B00002", record["targets"][0]["sourceBlockId"])
        self.assertEqual("chapter", record["format"])

    def test_bookmark_and_reference_mark_names_are_different_families(self):
        (count, records), _ = self.project('<text:p><text:reference-ref text:ref-name="X"/></text:p><text:h><text:bookmark-start text:name="X"/>Title</text:h>')
        self.assertEqual(1, count)
        self.assertEqual("target-not-found", records[0]["resolution"])

    def test_duplicate_anchor_remains_ambiguous(self):
        (_, records), _ = self.project('<text:p><text:reference-ref text:ref-name="X"/></text:p><text:h><text:reference-mark text:name="X"/>A</text:h><text:h><text:reference-mark text:name="X"/>B</text:h>')
        self.assertEqual("ambiguous-target", records[0]["resolution"])
        self.assertEqual(2, len(records[0]["targets"]))

    def test_peer_field_with_a_different_target_is_not_dropped(self):
        (_, records), _ = self.project('<text:p><text:reference-ref text:ref-name="Child"/><text:bookmark-ref text:ref-name="Parent">2.3.1</text:bookmark-ref></text:p><text:h><text:bookmark-start text:name="Parent"/>Parent</text:h><text:h><text:reference-mark text:name="Child"/>Child</text:h>')
        self.assertEqual(2, len(records))
        self.assertEqual([True, False], [record["cachedTextEmpty"] for record in records])
        self.assertNotEqual(records[0]["targets"][0]["sourceBlockId"], records[1]["targets"][0]["sourceBlockId"])

    def test_target_in_editorial_changes_needs_review(self):
        (_, records), _ = self.project('<text:p><text:reference-ref text:ref-name="X"/></text:p><text:h><text:reference-mark text:name="X"/>Title</text:h>', {"ODF-1.1/spec/B00002"})
        self.assertEqual("editorial-review-required", records[0]["resolution"])

    def test_no_owner_block_does_not_create_a_false_normative_unit(self):
        (_, records), _ = self.project('<text:p><text:reference-ref text:ref-name="X"/></text:p><text:reference-mark text:name="X"/>')
        self.assertEqual("target-outside-inventory-block", records[0]["resolution"])
        self.assertIsNone(records[0]["targets"][0]["sourceBlockId"])

    def test_offline_contract_cannot_replace_named_target(self):
        document, blocks = self.fixture()
        document["references"][0]["targets"][0]["name"] = "Something else"
        with self.assertRaisesRegex(ValueError, "different named-anchor"):
            refs.validate(document, blocks)

    def test_offline_contract_cannot_claim_rendered_chapter_numbers(self):
        document, blocks = self.fixture()
        document["renderedChapterNumbers"] = "verified"
        with self.assertRaisesRegex(ValueError, "completed semantics"):
            refs.validate(document, blocks)

    def test_offline_contract_cannot_hide_duplicate_targets(self):
        document, blocks = self.fixture()
        record = document["references"][0]
        record["targets"].append(copy.deepcopy(record["targets"][0]))
        with self.assertRaisesRegex(ValueError, "ambiguity"):
            refs.validate(document, blocks)


if __name__ == "__main__":
    unittest.main()
