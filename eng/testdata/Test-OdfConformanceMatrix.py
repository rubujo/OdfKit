"""Regression cases for the publication formats used by the conformance matrix."""

import importlib.util
import sys
import types
import unittest
from pathlib import Path
from unittest.mock import patch

sys.dont_write_bytecode = True

spec = importlib.util.spec_from_file_location("matrix", Path(__file__).resolve().parents[1] / "Build-OdfConformanceMatrix.py")
matrix = importlib.util.module_from_spec(spec)
spec.loader.exec_module(matrix)


class MatrixExtractionTests(unittest.TestCase):
    def test_schema_lock_accepts_checkout_line_endings_but_not_content_changes(self):
        for prefix in (b"", b"\xef\xbb\xbf"):
            lf = prefix + b'<grammar>\n  <empty/> \n</grammar>\n'
            self.assertEqual(matrix.schema_sha(lf), matrix.schema_sha(lf.replace(b"\n", b"\r\n")))
            self.assertNotEqual(matrix.schema_sha(lf), matrix.schema_sha(lf.replace(b"empty", b"text")))
            self.assertNotEqual(matrix.schema_sha(lf), matrix.schema_sha(lf.replace(b"/> ", b"/>")))

    def test_number_with_visual_margin_does_not_merge_into_title(self):
        rows = matrix.read_html('<h2><a id="scope"><span style="margin-right:0.1cm">1.1</span></a>Scope</h2><p>Documents shall declare their version.</p>')
        self.assertEqual(["1.1"], [row["clause"] for row in rows])
        self.assertEqual("Scope", rows[0]["title"])
        self.assertEqual(1, rows[0]["modalCount"])

    def test_appendix_paragraphs_are_included_from_local_toc(self):
        rows = matrix.read_html('<p><a href="#a">Appendix A Schemas</a></p><p><a href="#a1">Appendix A.1 Manifest Schema</a></p><p id="a">Appendix A Schemas</p><p id="a1">Manifest schema</p>')
        self.assertEqual(["A", "A.1"], [row["clause"] for row in rows])
        self.assertEqual("", rows[0]["proseSha256"])

    def test_duplicate_numbered_headings_are_rejected(self):
        with self.assertRaises(ValueError):
            matrix.read_html('<h1>1 Introduction</h1><h1>1 Different introduction</h1>')

    def test_non_heading_appendix_does_not_become_previous_clause_prose(self):
        rows = matrix.read_html('<a href="#a">Appendix A Schemas</a><h1>1 Introduction</h1><p>Documents shall declare a version.</p><p><a id="a"/>Appendix A Schemas</p><p>Other text must not enter clause 1.</p>')
        self.assertEqual(1, next(row for row in rows if row["clause"] == "1")["modalCount"])

    def test_pdf_toc_leader_spaces_do_not_swallow_following_clauses(self):
        toc = "Table of Contents\n1 Introduction.................. ........33\n1.1 Scope.......................... ..34\n"
        toc += "\n".join(f"2.{index} Clause {index}........ ...{index + 34}" for index in range(1, 800))
        pages = [types.SimpleNamespace(extract_text=lambda: toc),
                 types.SimpleNamespace(extract_text=lambda: "1 Introduction\nThis is the body.")]
        fake = types.SimpleNamespace(PdfReader=lambda _: types.SimpleNamespace(pages=pages))
        with patch.dict(sys.modules, {"pypdf": fake}):
            rows = matrix.read_pdf(Path("unused.pdf"))
        self.assertEqual(801, len(rows))
        self.assertEqual("Introduction", rows[0]["title"])
        self.assertEqual("Scope", rows[1]["title"])


if __name__ == "__main__":
    unittest.main()
