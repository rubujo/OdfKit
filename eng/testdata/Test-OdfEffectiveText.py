"""Regression tests for editorial redlines, ordinary strike and source ownership."""

import importlib.util
import sys
import unittest
from pathlib import Path
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("effective", Path(__file__).resolve().parents[1] / "Build-OdfEffectiveText.py")
effective = importlib.util.module_from_spec(spec)
spec.loader.exec_module(effective)


def style(name, family="text", parent="", **properties):
    return {"family": family, "name": name, "parent": parent,
            "textProperties": {effective.STYLE + key: value for key, value in properties.items()}}


class EffectiveTextTests(unittest.TestCase):
    def setUp(self):
        self.styles = {("text", "Deleted"): style("Deleted", **{"text-line-through-style": "solid"}),
                       ("text", "OrdinaryStrike"): style("OrdinaryStrike", **{"text-line-through-style": "solid"}),
                       ("text", "Reset"): style("Reset", **{"text-line-through-style": "none"}),
                       ("paragraph", "DeletedParagraph"): style("DeletedParagraph", "paragraph", **{"text-line-through-style": "solid"})}
        self.approved = {("text", "Deleted"), ("paragraph", "DeletedParagraph")}

    def project(self, content):
        xml = f'''<office:text xmlns:office="{effective.OFFICE[1:-1]}" xmlns:text="{effective.TEXT[1:-1]}"
        xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0">{content}</office:text>'''
        return effective.Projector(self.styles, self.approved).blocks(ET.fromstring(xml))

    def test_only_reviewed_editorial_style_is_deleted_and_tail_survives(self):
        blocks = self.project('<text:p>Keep <text:span text:style-name="Deleted">old</text:span> tail.</text:p>')
        block = next(iter(blocks.values()))
        self.assertEqual("Keep old tail.", block["rawText"])
        self.assertEqual("Keep tail.", block["effectiveText"])
        self.assertEqual(["text:Deleted"], block["deletionRuns"][0]["editorialStyles"])

    def test_ordinary_strike_formatting_is_preserved(self):
        block = next(iter(self.project('<text:p><text:span text:style-name="OrdinaryStrike">Example</text:span>.</text:p>').values()))
        self.assertEqual("Example.", block["effectiveText"])
        self.assertFalse(block["deletionRuns"])

    def test_explicit_none_overrides_inherited_editorial_strike(self):
        blocks = self.project('<text:p text:style-name="DeletedParagraph">old <text:span text:style-name="Reset">new</text:span> old</text:p>')
        block = next(iter(blocks.values()))
        self.assertEqual("new", block["effectiveText"])
        self.assertEqual(2, len(block["deletionRuns"]))

    def test_named_parent_style_can_carry_editorial_marking(self):
        self.styles[("text", "Derived")] = style("Derived", parent="Deleted")
        block = next(iter(self.project('<text:p>A<text:span text:style-name="Derived">x</text:span>B</text:p>').values()))
        self.assertEqual("AB", block["effectiveText"])

    def test_whole_deleted_paragraph_retains_source_block(self):
        block = next(iter(self.project('<text:p text:style-name="DeletedParagraph">Historical paragraph.</text:p>').values()))
        self.assertEqual("Historical paragraph.", block["rawText"])
        self.assertEqual("", block["effectiveText"])

    def test_nested_note_is_independent_and_not_counted_in_anchor_text(self):
        blocks = self.project('<text:p text:style-name="DeletedParagraph">Old<text:note><text:note-body><text:p>Independent note.</text:p></text:note-body></text:note></text:p>')
        self.assertEqual(2, len(blocks))
        self.assertEqual(["", "Independent note."], [block["effectiveText"] for block in blocks.values()])
        self.assertEqual(["Old", "Independent note."], [block["rawText"] for block in blocks.values()])

    def test_replacements_do_not_invent_spaces_between_word_fragments(self):
        block = next(iter(self.project('<text:p>draw<text:span text:style-name="Deleted">ing</text:span></text:p>').values()))
        self.assertEqual("drawing", block["rawText"])
        self.assertEqual("draw", block["effectiveText"])

    def test_spaces_tabs_and_line_breaks_match_original_normalization(self):
        block = next(iter(self.project('<text:p>A<text:s text:c="2"/>B<text:tab/>C<text:line-break/>D</text:p>').values()))
        self.assertEqual("A B C D", block["rawText"])
        self.assertEqual(block["rawText"], block["effectiveText"])

    def test_removed_tracking_storage_is_not_a_visible_block(self):
        blocks = self.project('<text:tracked-changes><text:p>Deleted store</text:p></text:tracked-changes><text:p>Body</text:p>')
        self.assertEqual(["Body"], [block["rawText"] for block in blocks.values()])

    def test_missing_or_cyclic_style_parent_fails_closed(self):
        self.styles[("text", "Loop")] = style("Loop", parent="Loop")
        projector = effective.Projector(self.styles, self.approved)
        with self.assertRaisesRegex(ValueError, "Cyclic"):
            projector.resolve(("text", "Loop"))
        with self.assertRaisesRegex(ValueError, "Missing"):
            projector.resolve(("text", "Absent"))


if __name__ == "__main__":
    unittest.main()
