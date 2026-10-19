"""Regressions for normative PDF code boundaries and lossless grammar comparison."""

import copy
import importlib.util
import sys
import unittest
from pathlib import Path

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("pdf_grammar", Path(__file__).resolve().parents[1] / "Build-OdfPdfGrammarEvidence.py")
grammar = importlib.util.module_from_spec(spec)
spec.loader.exec_module(grammar)
PREFIX = '<grammar xmlns="http://relaxng.org/ns/structure/1.0"'


class PdfGrammarTests(unittest.TestCase):
    def test_model_rejects_non_rng_root(self):
        with self.assertRaisesRegex(ValueError, "RELAX NG root"):
            grammar.canonical_model("<document/>")

    def test_unused_and_lexical_qname_namespace_bindings_are_compared(self):
        left = PREFIX + ' xmlns:x="urn:first"><ref name="x:entry"/></grammar>'
        right = left.replace("urn:first", "urn:second")
        with self.assertRaisesRegex(ValueError, "namespace"):
            grammar.compare_models(left, right, [])

    def test_scoped_namespace_rebinding_is_not_lost(self):
        left = PREFIX + ' xmlns:x="urn:first"><define xmlns:x="urn:second" name="x:entry"/></grammar>'
        right = left.replace('xmlns:x="urn:second"', 'xmlns:x="urn:third"')
        with self.assertRaisesRegex(ValueError, "namespace"):
            grammar.compare_models(left, right, [])

    def test_literal_space_is_not_discarded_as_indentation(self):
        left = PREFIX + '><value> </value></grammar>'
        right = PREFIX + '><value></value></grammar>'
        with self.assertRaisesRegex(ValueError, "Unreviewed"):
            grammar.compare_models(left, right, [])

    def test_pattern_operators_and_character_classes_cannot_be_normalized(self):
        left = PREFIX + '><data type="string"><param name="pattern">[ ]+</param></data></grammar>'
        right = left.replace("[ ]+", "[]+")
        with self.assertRaisesRegex(ValueError, "Unreviewed"):
            grammar.compare_models(left, right, [])

    def test_reviewed_wrap_removes_only_lf_and_preserves_spaces(self):
        left = PREFIX + '><data type="string"><param name="pattern">[ ]+\n[0-9]</param></data></grammar>'
        right = left.replace("\n", "")
        approved = [{"node": 2, "pdfTextSha256": grammar.sha(b"[ ]+\n[0-9]"), "rngTextSha256": grammar.sha(b"[ ]+[0-9]")}]
        count, digest = grammar.compare_models(left, right, approved)
        self.assertEqual(3, count)
        self.assertEqual(grammar.model_hash(grammar.canonical_model(right)), digest)
        approved[0]["pdfTextSha256"] = "stale"
        with self.assertRaisesRegex(ValueError, "Unreviewed"):
            grammar.compare_models(left, right, approved)

    def test_wrap_permission_cannot_apply_to_another_parameter(self):
        left = PREFIX + '><data type="string"><param name="minLength">1\n0</param></data></grammar>'
        right = left.replace("\n", "")
        approved = [{"node": 2, "pdfTextSha256": grammar.sha(b"1\n0"), "rngTextSha256": grammar.sha(b"10")}]
        with self.assertRaisesRegex(ValueError, "Unreviewed"):
            grammar.compare_models(left, right, approved)

    def test_stale_unused_wrap_permission_is_rejected(self):
        text = PREFIX + '><empty/></grammar>'
        with self.assertRaisesRegex(ValueError, "wrap inventory"):
            grammar.compare_models(text, text, [{"node": 1}])

    def test_bnf_text_inside_xml_is_not_a_valid_grammar_projection(self):
        text = PREFIX + '><define name="equation"><text/></define>number = digit | number digit</grammar>'
        with self.assertRaisesRegex(ValueError, "expression grammar"):
            grammar.canonical_model(text)

    def test_child_order_and_arity_are_preserved(self):
        left = PREFIX + '><choice><ref name="a"/><ref name="b"/></choice></grammar>'
        right = left.replace('name="a"', 'name="b"', 1)
        with self.assertRaisesRegex(ValueError, "Unreviewed"):
            grammar.compare_models(left, right, [])

    def test_shading_needs_margin_line_numbers_and_separate_panels(self):
        policy = {"left": 90., "right": 522.1, "numberLeft": 55, "numberFontSuffix": "+Thorndale"}
        rectangles = [{"x0": 90., "x1": 522.1, "y0": 100., "y1": 110.},
                      {"x0": 90., "x1": 522.1, "y0": 110., "y1": 120.},
                      {"x0": 90., "x1": 522.1, "y0": 140., "y1": 160.}]
        spans = [{"x": 60., "y": 105., "font": "/G+Thorndale", "text": "123"}]
        panels = grammar.numbered_panels(rectangles, spans, policy)
        self.assertEqual(2, len(panels))
        self.assertTrue(panels[0]["hasLineNumbers"])
        self.assertFalse(panels[1]["hasLineNumbers"])
        wrong_font = copy.deepcopy(spans)
        wrong_font[0]["font"] = "/Arial"
        self.assertFalse(any(panel["hasLineNumbers"] for panel in grammar.numbered_panels(rectangles, wrong_font, policy)))

    def test_code_projection_keeps_font_size_variants_and_fragment_order(self):
        panels = [{"x0": 90., "x1": 522.1, "y0": 100., "y1": 120., "hasLineNumbers": True},
                  {"x0": 90., "x1": 522.1, "y0": 140., "y1": 160., "hasLineNumbers": False}]
        spans = [{"x": 130., "y": 110., "text": 'entry"/>', "size": 9.1},
                 {"x": 90., "y": 110., "text": '<ref name="', "size": 10.},
                 {"x": 60., "y": 110., "text": '123', "size": 8.2},
                 {"x": 90., "y": 150., "text": 'number = digit', "size": 9.1},
                 {"x": 90., "y": 40., "text": 'footer', "size": 7.9}]
        self.assertEqual(['<ref name="entry"/>'], grammar.code_lines(panels, spans))


if __name__ == "__main__":
    unittest.main()
