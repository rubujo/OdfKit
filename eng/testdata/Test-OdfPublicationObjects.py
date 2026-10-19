"""Regressions for MathML source reconstruction and non-destructive rendition links."""

import importlib.util
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("objects", Path(__file__).resolve().parents[1] / "Build-OdfPublicationObjects.py")
objects = importlib.util.module_from_spec(spec)
spec.loader.exec_module(objects)


class ObjectEvidenceTests(unittest.TestCase):
    def test_local_reference_decodes_uri_once_and_rejects_traversal(self):
        self.assertEqual("Object 62", objects.local_reference("./Object%2062"))
        for href in (None, "https://example.com/object", "//example.com/object", "../outside", "%2e%2e/outside",
                     "/absolute", "dir\\outside", "Object#part", "Object?query"):
            self.assertIsNone(objects.local_reference(href))

    def test_only_mathml_root_is_promoted_to_mathematical_representation(self):
        self.assertIsNone(objects.mathml_evidence(b'<svg xmlns="http://www.w3.org/2000/svg"/>'))
        evidence = objects.mathml_evidence(b'<math xmlns="http://www.w3.org/1998/Math/MathML"><mi>N</mi></math>')
        self.assertEqual(2, evidence["nodeCount"])
        self.assertEqual([], evidence["foreignElements"])

    def test_annotation_does_not_override_presentation_tree(self):
        text = '<math xmlns="http://www.w3.org/1998/Math/MathML"><semantics><mi>N</mi><annotation encoding="StarMath 5.0">N</annotation></semantics></math>'
        original = objects.mathml_evidence(text.encode())
        changed = objects.mathml_evidence(text.replace("<mi>N</mi>", "<mi>x</mi>").encode())
        self.assertNotEqual(original["treeSha256"], changed["treeSha256"])
        self.assertEqual(original["annotations"], changed["annotations"])

    def test_namespace_qualified_and_unqualified_attributes_are_distinct(self):
        text = '<math xmlns="http://www.w3.org/1998/Math/MathML" xmlns:m="http://www.w3.org/1998/Math/MathML"><mfenced open="["><mi>N</mi></mfenced></math>'
        original = objects.mathml_evidence(text.encode())
        changed = objects.mathml_evidence(text.replace('open="["', 'm:open="["').encode())
        self.assertNotEqual(original["treeSha256"], changed["treeSha256"])

    def test_foreign_annotation_xml_is_preserved_as_an_explicit_review_dependency(self):
        text = '<math xmlns="http://www.w3.org/1998/Math/MathML"><semantics><mi>N</mi><annotation-xml><foreign xmlns="urn:other"/></annotation-xml></semantics></math>'
        evidence = objects.mathml_evidence(text.encode())
        self.assertEqual(["{urn:other}foreign"], evidence["foreignElements"])
        self.assertEqual("annotation-xml", evidence["annotations"][0]["kind"])

    def test_declared_alternatives_need_same_frame_order_and_supported_container_version(self):
        frame = ET.fromstring('<d:frame xmlns:d="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"><d:object/><d:image/></d:frame>')
        primary, alternate = list(frame)
        self.assertTrue(objects.frame_pair(frame, primary, alternate, "1.2"))
        self.assertTrue(objects.frame_pair(frame, primary, alternate, "1.3"))
        self.assertFalse(objects.frame_pair(frame, alternate, primary, "1.2"))
        self.assertFalse(objects.frame_pair(frame, primary, alternate, "9.9"))
        frame.append(ET.Element(objects.DRAW + "text-box"))
        self.assertFalse(objects.frame_pair(frame, primary, alternate, "1.2"))

    def test_accessibility_metadata_does_not_become_a_third_content_rendition(self):
        frame = ET.fromstring('<d:frame xmlns:d="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0" xmlns:s="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"><s:title>Equation</s:title><d:object/><d:image/></d:frame>')
        self.assertTrue(objects.frame_pair(frame, frame[1], frame[2], "1.3"))

    def test_locator_uses_original_local_name_indices(self):
        body = ET.fromstring('<text><p/><p><frame><object/><image/></frame></p></text>')
        path = '/office:document-content/office:body/office:text/p[2]/frame[1]/image[1]'
        self.assertEqual("image", objects.locate(body, path).tag)
        with self.assertRaisesRegex(ValueError, "locator root"):
            objects.locate(body, '/other/p[2]')


if __name__ == "__main__":
    unittest.main()
