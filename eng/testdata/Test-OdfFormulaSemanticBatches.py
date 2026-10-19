"""Reject incomplete, contradictory and out-of-scope function templates."""

import copy
import importlib.util
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("formula_roles", Path(__file__).resolve().parents[1] / "Build-OdfFormulaSemanticBatches.py")
roles = importlib.util.module_from_spec(spec)
spec.loader.exec_module(roles)


class FormulaRoleTests(unittest.TestCase):
    def setUp(self):
        self.group = {"id": "g", "memberSha256": "locked", "versions": ["1.4"],
                      "context": {"part": "formula", "headings": ["Standard Operators and Functions", "Text Functions", "UNICODE"]}}
        self.units = [{"id": f"ODF-1.4/formula/B{index:05d}/S001", "version": "1.4", "text": text,
                       "material": "heading" if index == 1 else "paragraph"} for index, text in enumerate([
                           "UNICODE", "Summary: Return a Unicode code point.", "Syntax: UNICODE( Text T )",
                           "Returns: Number", "Constraints: Length(T) > 0.",
                           "Semantics: Returns the first code point.", "The result may depend on normalization.",
                           "See also UNICHAR 6.20.25"], 1)]

    def compile(self):
        return roles.compile_group(self.group, self.units)

    def test_complete_roles_do_not_claim_numerical_review(self):
        compiled = self.compile()
        self.assertEqual("pending", compiled["semanticInterpretation"])
        self.assertEqual("pending", compiled["implementationAdjudication"])
        self.assertEqual(len(self.units), len(compiled["bindings"]))
        self.assertEqual(["may"], compiled["bindings"][6]["scopeSignals"])
        self.assertFalse(compiled["crossVersionEquivalenceClaim"])

    def test_upstream_signature_name_mismatch_is_not_an_alias(self):
        self.units[2]["text"] = "Syntax: ISERR( Any X )"
        self.assertIsNone(self.compile())

    def test_required_semantics_cannot_be_missing(self):
        self.units = self.units[:5]
        self.assertIsNone(self.compile())

    def test_duplicate_section_is_not_a_continuation(self):
        self.units[6]["text"] = "Semantics: Another definition."
        self.assertIsNone(self.compile())

    def test_unknown_section_cannot_be_swallowed_as_semantics(self):
        self.units[6]["text"] = "Implementation: Something else."
        self.assertIsNone(self.compile())

    def test_table_requires_separate_semantic_review(self):
        self.units[6]["material"] = "table-cell"
        self.assertIsNone(self.compile())

    def test_unread_object_prevents_whole_group_classification(self):
        self.units.append({"id": "ODF-1.4/formula/O00001", "version": "1.4", "text": "", "material": "object"})
        self.assertIsNone(self.compile())

    def test_paragraph_is_not_a_source_heading(self):
        self.units[0]["sourceKind"] = "paragraph"
        self.assertIsNone(self.compile())

    def test_host_function_requires_special_scope_review(self):
        self.group["context"]["headings"][-1] = "DDE"
        self.assertIsNone(self.compile())

    def test_one_incomplete_version_keeps_entire_group_pending(self):
        self.group["versions"].append("1.3")
        self.assertIsNone(self.compile())

    def test_no_reordering_across_versions(self):
        additional = copy.deepcopy(self.units)
        for unit in additional:
            unit.update(id=unit["id"].replace("1.4", "1.3"), version="1.3")
        additional[2]["text"] = "Syntax: UNICODE( Any X )"
        self.group["versions"].append("1.3")
        self.units += additional
        result = self.compile()
        self.assertEqual(16, len(result["bindings"]))
        self.assertNotEqual(result["bindings"][2]["predicateSha256"], result["bindings"][10]["predicateSha256"])

    def test_arbitrary_return_prose_is_not_a_declared_type(self):
        self.units[3]["text"] = "Returns: Depends on the user interface"
        self.assertIsNone(self.compile())

    def test_text_after_see_also_requires_review(self):
        self.units.append({"id": "ODF-1.4/formula/B00009/S001", "version": "1.4", "material": "paragraph", "text": "Unrelated appendix."})
        self.assertIsNone(self.compile())

    def test_signature_delimiters_are_balanced(self):
        self.assertFalse(roles.signature("X", "X( Number A [; Number B )"))
        self.assertTrue(roles.signature("X", "X( Number A [; Number B = 0 ] )"))
        self.assertFalse(roles.signature("X", "X( Number A ) or Y( A )"))

    def test_empty_dependency_heading_is_an_upstream_gap(self):
        self.units[-1]["text"] = "See also"
        self.assertIsNone(self.compile())

    def contract_fixture(self):
        compiled = self.compile()
        document = {"schemaVersion": 1, "fieldPolicy": copy.deepcopy(roles.POLICY), "conditions": roles.CONDITIONS,
                    "fullConformanceClaim": False, "semanticClassificationComplete": False,
                    "numericalSemanticsReviewed": False, "groups": [compiled]}
        rows = {unit["id"]: {"version": unit["version"], "sentenceSha256": roles.digest(unit["text"].encode())} for unit in self.units}
        classified = {unit["id"]: {"groupId": "g", "material": unit["material"]} for unit in self.units}
        return document, {"g": self.group}, rows, classified, set()

    def test_offline_contract_keeps_section_roles_separate_from_semantic_review(self):
        self.assertEqual({"g"}, roles.validate_contracts(*self.contract_fixture()))

    def test_offline_contract_rejects_claimed_numerical_review(self):
        arguments = self.contract_fixture()
        arguments[0]["numericalSemanticsReviewed"] = True
        with self.assertRaisesRegex(ValueError, "completion"):
            roles.validate_contracts(*arguments)

    def test_offline_contract_rejects_partial_group(self):
        arguments = self.contract_fixture()
        arguments[0]["groups"][0]["bindings"].pop(6)
        with self.assertRaisesRegex(ValueError, "Partial"):
            roles.validate_contracts(*arguments)

    def test_offline_contract_rejects_changed_signature_marker(self):
        arguments = self.contract_fixture()
        arguments[0]["groups"][0]["bindings"][2]["markerText"] = "Syntax: ISERR( Any X )"
        with self.assertRaisesRegex(ValueError, "marker changed"):
            roles.validate_contracts(*arguments)

    def test_offline_contract_rejects_reassigned_continuation(self):
        arguments = self.contract_fixture()
        arguments[0]["groups"][0]["bindings"][6]["field"] = "Constraints"
        with self.assertRaisesRegex(ValueError, "responsibility changed"):
            roles.validate_contracts(*arguments)

    def test_offline_contract_rejects_false_sdk_exemption(self):
        arguments = self.contract_fixture()
        arguments[0]["fieldPolicy"]["Semantics"]["exemption"] = "outside-sdk"
        with self.assertRaisesRegex(ValueError, "completion"):
            roles.validate_contracts(*arguments)

    def test_offline_contract_rejects_double_count_with_manual_review(self):
        arguments = self.contract_fixture()
        arguments[-1].add("g")
        with self.assertRaisesRegex(ValueError, "manually reviewed"):
            roles.validate_contracts(*arguments)

    def typography(self, styles, body):
        xml = f'<root xmlns:style="{roles.STYLE[1:-1]}" xmlns:text="{roles.TEXT[1:-1]}">{styles}</root>'
        resolver = roles.position_resolver([ET.fromstring(xml)])
        paragraph = ET.fromstring(f'<text:p xmlns:text="{roles.TEXT[1:-1]}">{body}</text:p>')
        return roles.positioned_text(paragraph, resolver)

    def test_superscript_digits_remain_distinct_from_flat_number(self):
        spans = self.typography('<style:style style:name="S" style:family="text"><style:text-properties style:text-position="super 58%"/></style:style>',
                                '−2<text:span text:style-name="S">39</text:span>')
        self.assertEqual([("39", "super 58%")], [(span["text"], span["position"]) for span in spans])

    def test_position_inherits_through_named_styles(self):
        spans = self.typography('<style:style style:name="S" style:family="text"><style:text-properties style:text-position="sub 58%"/></style:style>'
                                '<style:style style:name="Child" style:family="text" style:parent-style-name="S"/>',
                                'a<text:span text:style-name="Child">1</text:span>')
        self.assertEqual("sub 58%", spans[0]["position"])
        self.assertEqual(["S", "Child"], [item["name"] for item in spans[0]["styleChain"]])

    def test_explicit_baseline_cancels_inherited_superscript(self):
        spans = self.typography('<style:style style:name="S" style:family="text"><style:text-properties style:text-position="super 58%"/></style:style>'
                                '<style:style style:name="B" style:family="text"><style:text-properties style:text-position="0% 100%"/></style:style>',
                                '<text:span text:style-name="S">a<text:span text:style-name="B">b</text:span>c</text:span>')
        self.assertEqual(["a", "c"], [span["text"] for span in spans])

    def test_numeric_baseline_shift_cannot_be_discarded(self):
        spans = self.typography('<style:style style:name="S" style:family="text"><style:text-properties style:text-position="33% 80%"/></style:style>',
                                '10<text:span text:style-name="S">−Digits</text:span>')
        self.assertEqual("33% 80%", spans[0]["position"])

    def test_cyclic_style_cannot_silently_flatten_a_formula(self):
        with self.assertRaisesRegex(ValueError, "Cyclic"):
            self.typography('<style:style style:name="S" style:family="text" style:parent-style-name="S"/>',
                            '<text:span text:style-name="S">39</text:span>')

    def test_explicit_union_signature_keeps_both_type_branches(self):
        self.units[2]["text"] = "Syntax: UNICODE( Reference|Array X )"
        self.assertIsNotNone(self.compile())

    def test_operator_reference_list_is_a_dependency_not_unreviewed_prose(self):
        self.units[-1]["text"] = 'See also FIND 6.20.9, Infix Operator "=" 6.4.7, Infix Operator Ordered Comparison ("<", "<=", ">", ">=") 6.4.9'
        self.assertEqual("See also", self.compile()["bindings"][-1]["field"])

    def test_function_note_remains_behavior_not_an_informative_waiver(self):
        self.units[6]["text"] = "Note: X shall only be computed once."
        binding = self.compile()["bindings"][6]
        self.assertEqual("Semantics", binding["field"])
        self.assertEqual(["only", "shall"], binding["scopeSignals"])
        self.assertEqual("none", roles.POLICY[binding["field"]]["exemption"])

    def test_alternative_signatures_must_name_the_same_function(self):
        self.assertTrue(roles.signature("X", "X( Complex A ) or X( Number A )"))
        self.assertFalse(roles.signature("X", "X( Complex A ) or Y( Number A )"))

    def test_equivalent_formula_intro_requires_semantics_context(self):
        self.units[6]["text"] = "This is equivalent to:"
        self.assertIsNotNone(self.compile())
        self.units[1]["text"] = "Summary: Test."
        self.units.insert(2, {"id": "ODF-1.4/formula/B00002/S002", "version": "1.4", "text": "This is equivalent to:", "material": "paragraph"})
        self.assertIsNone(self.compile())

    def test_named_dependency_without_section_keeps_resolution_pending(self):
        self.units[-1]["text"] = "See also IMSINH, CSCH"
        binding = self.compile()["bindings"][-1]
        self.assertEqual(["IMSINH", "CSCH"], binding["unresolvedNamedReferences"])
        self.assertEqual([], binding["referencedSections"])


if __name__ == "__main__":
    unittest.main()
