"""Regression tests for lossless review-unit extraction and false-proof guards."""

import importlib.util
import io
import sys
import unittest
import zipfile
from pathlib import Path

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("inventory", Path(__file__).resolve().parents[1] / "Build-OdfObligationInventory.py")
inventory = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inventory)


class ObligationExtractionTests(unittest.TestCase):
    def test_declarative_sentences_and_conditional_fragments_are_not_dropped(self):
        text = "The default is 1.4. If the entry exists, then it is stored. A list item without a modal"
        units = inventory.sentences(text)
        self.assertEqual(3, len(units))
        self.assertEqual(text, " ".join(unit[2] for unit in units))

    def test_abbreviations_urls_and_decimal_values_do_not_create_false_boundaries(self):
        text = "Examples, e.g. A value, use https://example.test/1.4. A new sentence follows."
        self.assertEqual(2, len(inventory.sentences(text)))

    def test_odt_spaces_cells_and_nested_notes_have_exactly_one_block_each(self):
        xml = '''<office:document-content
        xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
        xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
        xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0">
        <office:body><office:text><text:table-of-content><text:p>TOC copy</text:p></text:table-of-content>
        <text:h text:outline-level="1">Requirements</text:h>
        <text:p>A<text:s text:c="2"/>value<text:note><text:note-body><text:p>Note.</text:p></text:note-body></text:note> follows.</text:p>
        <table:table><table:table-row><table:table-cell><text:p>Cell requirement.</text:p></table:table-cell></table:table-row></table:table>
        <text:tracked-changes><text:p>Removed text</text:p></text:tracked-changes>
        </office:text></office:body></office:document-content>'''
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, "w") as package:
            package.writestr("content.xml", xml)
        stream.seek(0)
        blocks, exclusions, objects = inventory.odt_blocks(stream)
        self.assertEqual(["Requirements", "A value follows.", "Note.", "Cell requirement."], [block["text"] for block in blocks])
        self.assertEqual(2, len(exclusions))
        self.assertEqual([], objects)
        self.assertEqual(len(blocks), len({block["locator"] for block in blocks}))
        self.assertTrue(all(block["headingPath"] == ["Requirements"] for block in blocks))

    def test_heading_paths_reset_for_siblings_and_follow_opaque_objects(self):
        xml = '''<office:document-content
        xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
        xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
        xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0">
        <office:body><office:text>
        <text:h text:outline-level="1">Document Structure</text:h>
        <text:h text:outline-level="2">Nested</text:h><text:p>First.</text:p>
        <text:h text:outline-level="1">Tables</text:h><text:p>Second.<draw:image/></text:p>
        </office:text></office:body></office:document-content>'''
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, "w") as package:
            package.writestr("content.xml", xml)
        stream.seek(0)
        blocks, _, objects = inventory.odt_blocks(stream)
        self.assertEqual(["Document Structure", "Nested"], blocks[2]["headingPath"])
        self.assertEqual(["Tables"], blocks[4]["headingPath"])
        self.assertEqual(["Tables"], objects[0]["headingPath"])

    def test_manual_review_cannot_float_to_a_changed_or_duplicate_sentence(self):
        row = {"id": "unit", "sentenceSha256": "original"}
        review = {"id": "decision", "status": "partial", "scope": "validator", "requirement": "Requirement",
                  "assessment": "Evidence remains partial", "units": [{"id": "unit", "sentenceSha256": "different"}]}
        with self.assertRaisesRegex(ValueError, "sentence changed"):
            inventory.apply_reviews([row], {"decisions": [review]})
        review["units"][0]["sentenceSha256"] = "original"
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            inventory.apply_reviews([row], {"decisions": [review, review]})

    def test_fulfilled_review_requires_implementation_and_regression_evidence(self):
        review = {"id": "decision", "status": "fulfilled", "scope": "validator", "requirement": "Requirement",
                  "assessment": "Claim", "implementation": [], "tests": [],
                  "units": [{"id": "unit", "sentenceSha256": "original"}]}
        with self.assertRaisesRegex(ValueError, "no evidence"):
            inventory.apply_reviews([{"id": "unit", "sentenceSha256": "original"}], {"decisions": [review]})


if __name__ == "__main__":
    unittest.main()
