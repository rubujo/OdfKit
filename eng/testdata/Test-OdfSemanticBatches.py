"""Regressions for complete, source-bound semantic family compilation."""

import copy
import importlib.util
import json
import sys
import unittest
from pathlib import Path

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("batches", Path(__file__).resolve().parents[1] / "Build-OdfSemanticBatches.py")
batches = importlib.util.module_from_spec(spec)
spec.loader.exec_module(batches)


class SemanticBatchTests(unittest.TestCase):
    def setUp(self):
        self.unit = "ODF-1.4/schema/B00002/S001"
        self.text = "The text:name attribute has the data type string 18.2."
        self.group = {"id": "g", "memberSha256": "locked", "versions": ["1.4"],
                      "context": {"part": "schema", "headings": ["General Attributes", "text:name"]}}
        self.row = {"id": self.unit, "version": "1.4", "part": "schema", "start": "0",
                    "end": str(len(self.text)), "sentenceSha256": batches.digest(self.text.encode())}
        self.classification = {"material": "paragraph", "groupId": "g"}
        self.contexts = {self.unit.rsplit("/S", 1)[0]: {"kind": "paragraph", "text": self.text}}
        self.bounds = {"1.4": (1, 4)}

    def compile(self, text=None, material="paragraph"):
        row = self.row.copy()
        if text is not None:
            row.update(end=str(len(text)), sentenceSha256=batches.digest(text.encode()))
            self.contexts[self.unit.rsplit("/S", 1)[0]]["text"] = text
        return batches.compile_group(self.group, [(row, {**self.classification, "material": material})], self.contexts, self.bounds)

    def contract_fixture(self):
        bindings = self.compile()
        metadata = {"schemaVersion": 1, "fullConformanceClaim": False, "builderSha256": batches.code_hash(),
                    "familySha256": batches.family_hash(), "families": copy.deepcopy(batches.FAMILIES),
                    "unitCount": 1, "groups": [{"groupId": "g", "memberSha256": "locked", "versions": ["1.4"],
                        "implementationAdjudication": "pending", "crossVersionEquivalenceClaim": False,
                        "versionAssessment": "Only 1.4 reviewed"}]}
        markers = []
        rows = {self.unit: self.row}
        for number, title in ((1, "Document Structure"), (4, "OpenDocument Relax NG Schema")):
            identifier = f"ODF-1.4/schema/B{number:05d}"
            marker_hash = batches.digest(title.encode())
            markers.append({"id": identifier, "title": title, "textSha256": marker_hash})
            rows[identifier + "/S001"] = {"blockSha256": marker_hash}
        policy = {"schemaVersion": 1, "ranges": [{"version": "1.4", "part": "schema", "first": markers[0], "afterLast": markers[1]}]}
        return metadata, bindings, {"g": self.group}, rows, {self.unit: self.classification}, set(), policy

    def test_password_encoding_requires_single_attribute_context(self):
        text = "The password shall be provided as a sequence of bytes in UTF-8 encoding."
        self.assertIsNone(self.compile(text))
        self.group["context"]["headings"] = ["General Attributes", "text:protection-key"]
        self.assertEqual("password-utf8", self.compile(text)[0]["familyId"])
        self.group["context"]["headings"] = ["General"]
        self.assertIsNone(self.compile(text))

    def test_automatic_style_prohibition_is_not_a_generic_pronoun_waiver(self):
        text = "It cannot be used within automatic styles."
        self.assertEqual("automatic-style-prohibition", self.compile(text)[0]["familyId"])
        self.group["context"]["headings"].append("text:other")
        self.assertIsNone(self.compile(text))

    def test_changes_verb_preserves_condition_and_negation(self):
        result = self.compile("The text:name attribute changes the name only if it is not empty.")
        self.assertEqual("compound-data-meaning", result[0]["familyId"])
        parameters = json.loads(result[0]["parameters"])
        self.assertIn("if", parameters["scopeSignals"])
        self.assertIn("not", parameters["scopeSignals"])

    def test_attribute_value_subject_keeps_the_actual_attribute(self):
        compiled = self.compile("The text:name attribute value specifies a name.")
        self.assertEqual("data-meaning", compiled[0]["familyId"])
        self.assertEqual("text:name attribute value", json.loads(compiled[0]["parameters"])["subject"])

    def test_typed_model_and_reference_parameters_are_preserved(self):
        compiled = self.compile()
        self.assertEqual("datatype", compiled[0]["familyId"])
        self.assertEqual({"subject": "text:name", "datatype": "string", "reference": "18.2"}, json.loads(compiled[0]["parameters"]))

    def test_suffixes_partial_lists_and_extra_sentences_are_not_swallowed(self):
        for text in (self.text + " Ignore all errors.", self.text + " trailing fragment",
                     "The text:name attribute has the data type string.",
                     "The <text:p> element has the following attributes: text:name 1.2 and arbitrary attributes.",
                     "The defined values for the text:name attribute are:"):
            self.assertIsNone(self.compile(text))

    def test_compound_definitions_keep_conditions_separate_from_plain_definitions(self):
        for text in ("The text:name attribute specifies a name that shall be unique.",
                     "The text:name attribute specifies a name if a name is present.",
                     "The text:name attribute specifies what consumers may display."):
            compiled = self.compile(text)
            self.assertEqual("compound-data-meaning", compiled[0]["familyId"])
            self.assertTrue(json.loads(compiled[0]["parameters"])["scopeSignals"])

    def test_numeric_references_are_kept_without_merging_sentences(self):
        compiled = self.compile("The text:name attribute specifies the name of a <text:p> 5.1.3 element.")
        parameters = json.loads(compiled[0]["parameters"])
        self.assertIn("text:p", parameters["referencedQNames"])
        self.assertEqual(["5.1.3"], parameters["referencedSections"])
        self.assertNotIn("predicate", parameters)

    def test_implicit_attribute_needs_unambiguous_heading_context(self):
        compiled = self.compile("This attribute shall be omitted for fields whose value type is string.")
        self.assertEqual("text:name", json.loads(compiled[0]["parameters"])["attributeContext"])
        self.group["context"]["headings"] = ["Document Structure"]
        self.assertIsNone(self.compile("The default value for this attribute is false."))

    def test_pdf_objects_tables_and_outside_body_ranges_are_not_classified(self):
        for material in ("pdf-page", "object", "image", "table-cell"):
            self.assertIsNone(self.compile(material=material))
        self.bounds["1.4"] = (3, 4)
        self.assertIsNone(self.compile())

    def test_one_unknown_member_keeps_entire_group_pending(self):
        extra_row = {**self.row, "id": "ODF-1.4/schema/B00003/S001"}
        self.contexts["ODF-1.4/schema/B00003"] = {"text": "Unreviewed semantic condition.", "kind": "paragraph"}
        extra_row.update(end=str(len("Unreviewed semantic condition.")), sentenceSha256=batches.digest(b"Unreviewed semantic condition."))
        self.assertIsNone(batches.compile_group(self.group,
            [(self.row, self.classification), (extra_row, self.classification)], self.contexts, self.bounds))

    def test_changed_text_or_promoted_paragraph_fails(self):
        self.contexts[self.unit.rsplit("/S", 1)[0]]["text"] = "Changed."
        with self.assertRaisesRegex(ValueError, "sentence changed"):
            self.compile()
        with self.assertRaisesRegex(ValueError, "promoted"):
            self.compile(material="heading")

    def test_offline_contracts_bind_complete_members_and_source_hashes(self):
        fixture = self.contract_fixture()
        self.assertEqual({"g"}, batches.validate_contracts(*fixture))
        fixture[1][0]["sourceUnitSha256"] = "stale"
        with self.assertRaisesRegex(ValueError, "sentence changed"):
            batches.validate_contracts(*fixture)

    def test_closed_group_can_keep_its_source_bound_family_review(self):
        fixture = self.contract_fixture()
        fixture[2]["g"]["requiresFollowUp"] = False
        self.assertEqual({"g"}, batches.validate_contracts(*fixture))

    def test_offline_contracts_reject_duplicate_and_missing_members(self):
        fixture = self.contract_fixture()
        fixture[1].append(copy.deepcopy(fixture[1][0]))
        fixture[0]["unitCount"] = 2
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            batches.validate_contracts(*fixture)
        fixture = self.contract_fixture()
        fixture[1].clear()
        fixture[0]["unitCount"] = 0
        with self.assertRaisesRegex(ValueError, "partially understood"):
            batches.validate_contracts(*fixture)

    def test_offline_contracts_reject_heading_waivers_or_manual_overlap(self):
        fixture = self.contract_fixture()
        fixture[1][0]["familyId"] = "heading"
        with self.assertRaisesRegex(ValueError, "waived as a heading"):
            batches.validate_contracts(*fixture)
        fixture = self.contract_fixture()
        fixture[5].add("g")
        with self.assertRaisesRegex(ValueError, "manually reviewed"):
            batches.validate_contracts(*fixture)

    def test_contracts_reject_fabricated_sdk_exclusions_and_completion(self):
        fixture = self.contract_fixture()
        fixture[0]["families"]["datatype"]["exemption"] = "outside-sdk"
        with self.assertRaisesRegex(ValueError, "families changed"):
            batches.validate_contracts(*fixture)
        fixture = self.contract_fixture()
        fixture[0]["groups"][0]["implementationAdjudication"] = "fulfilled"
        with self.assertRaisesRegex(ValueError, "cannot close"):
            batches.validate_contracts(*fixture)

    def test_cross_version_boundary_substitution_is_rejected(self):
        fixture = self.contract_fixture()
        fixture[6]["ranges"][0]["first"]["id"] = "ODF-1.2/schema/B00001"
        with self.assertRaisesRegex(ValueError, "boundary version"):
            batches.validate_contracts(*fixture)

    def test_enumeration_needs_own_introduction_and_nonempty_cases(self):
        self.assertIsNone(self.compile("false: cell is not protected."))
        self.assertIsNone(self.compile("The defined values for the text:name attribute are:"))
        intro = {"unitId": "ODF-1.4/schema/B00002/S001", "familyId": "enumeration-introduction",
                 "parameters": json.dumps({"subject": "text:name"})}
        value = {"unitId": "ODF-1.4/schema/B00003/S001", "familyId": "enumeration-value",
                 "parameters": json.dumps({"attributeContext": "text:name"})}
        self.assertTrue(batches.complete_enumerations([intro, value]))
        value["parameters"] = json.dumps({"attributeContext": "table:name"})
        self.assertFalse(batches.complete_enumerations([intro, value]))

    def test_enumeration_cannot_borrow_previous_version_introduction(self):
        intro = {"unitId": "ODF-1.3/schema/B00002/S001", "familyId": "enumeration-introduction",
                 "parameters": json.dumps({"subject": "text:name"})}
        value = {"unitId": "ODF-1.4/schema/B00003/S001", "familyId": "enumeration-value",
                 "parameters": json.dumps({"attributeContext": "text:name"})}
        self.assertFalse(batches.complete_enumerations([intro, value]))

    def test_notes_and_compound_enum_branches_keep_distinct_semantics(self):
        self.assertIsNone(batches.parse_sentence("paragraph", "Note: this has special handling."))
        compound = batches.parse_sentence("paragraph", "true: the consumer shall draw the image.")
        self.assertEqual("conditional-enumeration-value", compound[0])
        self.assertEqual(["shall"], compound[1]["scopeSignals"])
        self.assertIsNone(self.compile("true: the consumer shall draw the image."))
        parsed = batches.parse_sentence("paragraph", "false: cell is not protected.")
        self.assertEqual("enumeration-value", parsed[0])

    def test_conditional_enumeration_preserves_condition_and_target(self):
        family, parameters = batches.parse_sentence("paragraph", "If the value of the number:format-source attribute is fixed the number:style attribute on the same element has the defined values:")
        self.assertEqual("conditional-enumeration-introduction", family)
        self.assertEqual("number:format-source", parameters["conditionAttribute"])
        self.assertEqual("fixed", parameters["conditionValue"])
        self.assertEqual("number:style", parameters["subject"])
        intro = {"unitId": "ODF-1.4/schema/B00002/S001", "familyId": family, "parameters": json.dumps(parameters)}
        value = {"unitId": "ODF-1.4/schema/B00003/S001", "familyId": "conditional-enumeration-value",
                 "parameters": json.dumps({"attributeContext": "number:style"})}
        self.assertTrue(batches.complete_enumerations([intro, value]))
        value["parameters"] = json.dumps({"attributeContext": "number:format-source"})
        self.assertFalse(batches.complete_enumerations([intro, value]))

    def test_default_marker_requires_immediately_preceding_same_version_value(self):
        self.assertIsNone(self.compile("This is the default setting."))
        intro = {"unitId": "ODF-1.4/schema/B00001/S001", "familyId": "enumeration-introduction",
                 "parameters": json.dumps({"subject": "text:name"})}
        value = {"unitId": "ODF-1.4/schema/B00002/S001", "familyId": "enumeration-value",
                 "parameters": json.dumps({"attributeContext": "text:name"})}
        marker = {"unitId": "ODF-1.4/schema/B00003/S001", "familyId": "enumeration-default",
                  "parameters": json.dumps({"valueUnitId": value["unitId"]})}
        self.assertTrue(batches.complete_enumerations([intro, value, marker]))
        marker["parameters"] = json.dumps({"valueUnitId": intro["unitId"]})
        self.assertFalse(batches.complete_enumerations([intro, value, marker]))
        marker["parameters"] = json.dumps({"valueUnitId": value["unitId"]})
        marker["unitId"] = "ODF-1.3/schema/B00003/S001"
        self.assertFalse(batches.complete_enumerations([intro, value, marker]))

    def test_informative_range_marker_must_be_explicit_and_same_version(self):
        policy = copy.deepcopy(self.contract_fixture()[-1])
        first = {"id": "ODF-1.4/schema/B00010", "title": "Accessibility Guidelines (Non Normative)"}
        last = {"id": "ODF-1.4/schema/B00020", "title": "Other Appendix (Non Normative)"}
        for marker in (first, last):
            marker["textSha256"] = batches.digest(marker["title"].encode())
        policy["informativeContexts"] = [{"first": first, "afterLast": last}]
        batches.validate_ranges(policy)
        last["id"] = "ODF-1.3/schema/B00020"
        with self.assertRaisesRegex(ValueError, "informative context boundary"):
            batches.validate_ranges(policy)

    def test_informative_markers_are_bound_to_original_inventory(self):
        fixture = self.contract_fixture()
        first = {"id": "ODF-1.4/schema/B00010", "title": "Accessibility Guidelines (Non Normative)"}
        last = {"id": "ODF-1.4/schema/B00020", "title": "Other Appendix (Non Normative)"}
        for marker in (first, last):
            marker["textSha256"] = batches.digest(marker["title"].encode())
        fixture[-1]["informativeContexts"] = [{"first": first, "afterLast": last}]
        with self.assertRaisesRegex(ValueError, "Informative boundary"):
            batches.validate_contracts(*fixture)

    def test_rng_fragments_keep_operators_and_namespaced_annotations(self):
        family, parameters = batches.parse_sentence("paragraph", '<attribute name="table:case-sensitive" a:defaultValue="true">')
        self.assertEqual("rng-fragment", family)
        self.assertEqual("open", parameters["action"])
        self.assertEqual("table:case-sensitive", parameters["nodes"][0]["attributes"]["name"])
        self.assertEqual("true", parameters["nodes"][0]["attributes"]["{http://relaxng.org/ns/compatibility/annotations/1.0}defaultValue"])
        self.assertEqual("close", batches.parse_rng('</optional>')[1]["action"])

    def test_instance_examples_and_prose_cannot_hide_in_rng_tokens(self):
        for text in ('<office:document/>', '<unknown/>', '<empty/> plus a prose requirement',
                     '<optional>Consumers shall display it.</optional>',
                     '<optional><!-- MUST NOT be ignored --></optional>',
                     '<!-- one --> tail <!-- two -->'):
            self.assertIsNone(batches.parse_rng(text))

    def test_schema_comments_keep_prose_obligation_signals(self):
        family, parameters = batches.parse_rng("<!-- The value must match XSL's clip. -->")
        self.assertEqual("rng-annotation", family)
        self.assertEqual(["must"], parameters["scopeSignals"])
        self.assertIsNone(self.compile("<!-- The value must match XSL's clip. -->"))

    def test_rng_source_needs_reviewed_style_not_just_xml_vocabulary(self):
        self.assertIsNone(self.compile('<ref name="string"/>'))
        self.contexts[self.unit.rsplit("/S", 1)[0]].update(codeStyleRoot="RelaxNG", codeStyleProfile="document-grammar")
        compiled = self.compile('<ref name="string"/>')
        parameters = json.loads(compiled[0]["parameters"])
        self.assertEqual("document-grammar", parameters["sourceCodeProfile"])

    def test_changed_redline_or_example_group_needs_explicit_review(self):
        fixture = self.contract_fixture()
        with self.assertRaisesRegex(ValueError, "Unreconciled editorial"):
            batches.validate_contracts(*fixture, changed_blocks={self.unit.rsplit("/S", 1)[0]})
        self.group["context"]["headings"].append("Example")
        self.assertFalse(batches.eligible_group(self.group, [self.unit], set(), set()))

    def test_split_rng_tag_is_reconstructed_with_both_original_source_ids(self):
        texts = ['<define name="paragraph"', 'combine="interleave">']
        members, contexts = [], {}
        for number, text in enumerate(texts, 2):
            unit = f"ODF-1.4/schema/B{number:05d}/S001"
            row = {**self.row, "id": unit, "end": str(len(text)), "sentenceSha256": batches.digest(text.encode())}
            members.append((row, self.classification))
            contexts[unit.rsplit("/S", 1)[0]] = {"kind": "paragraph", "text": text,
                "codeStyleRoot": "RelaxNG", "codeStyleProfile": "document-grammar"}
        bindings = batches.compile_group(self.group, members, contexts, self.bounds)
        self.assertEqual(2, len(bindings))
        parameters = json.loads(bindings[0]["parameters"])
        self.assertEqual([member[0]["id"] for member in members], parameters["sourceFragmentUnits"])
        self.assertEqual("interleave", parameters["nodes"][0]["attributes"]["combine"])
        self.assertEqual(bindings[0]["parameters"], bindings[1]["parameters"])

    def test_split_tag_cannot_cross_code_style_or_source_gap(self):
        texts = ['<define name="paragraph"', 'combine="interleave">']
        prepared = []
        for number, text in enumerate(texts, 2):
            row = {**self.row, "id": f"ODF-1.4/schema/B{number:05d}/S001"}
            prepared.append((row, self.classification, {"codeStyleRoot": "RelaxNG", "codeStyleProfile": "document-grammar"}, text))
        prepared[1][2]["codeStyleProfile"] = "example"
        self.assertIsNone(batches.stitch_fragment(prepared, 0))
        prepared[1][2]["codeStyleProfile"] = "document-grammar"
        prepared[1][0]["id"] = "ODF-1.4/schema/B00005/S001"
        self.assertIsNone(batches.stitch_fragment(prepared, 0))

    def test_legacy_pronoun_context_requires_unique_schema_attribute(self):
        group = {"context": {"headings": ["Display Name"]}}
        declaration = batches.parse_rng('<attribute name="style:display-name">')
        self.assertEqual("style:display-name", batches.attribute_context(group, [declaration]))
        other = batches.parse_rng('<attribute name="style:name">')
        self.assertIsNone(batches.attribute_context(group, [declaration, other]))
        self.assertIsNone(batches.attribute_context(group, []))

    def test_element_heading_qname_cannot_become_attribute_context(self):
        group = {"context": {"headings": ["Text", "text:p"]}}
        self.assertIsNone(batches.attribute_context(group, []))
        attribute = batches.parse_sentence("paragraph", "The text:name attribute has the data type string 18.2.")
        self.assertEqual("text:name", batches.attribute_context(group, [attribute]))

    def test_explicit_primary_attribute_can_coexist_with_related_grammar_attributes(self):
        group = {"context": {"headings": ["Metadata", "Template Modification Date and Time"]}}
        primary = batches.parse_sentence("paragraph", "The meta:date attribute specifies the date and time of the template.")
        own = batches.parse_rng('<attribute name="meta:date">')
        related = batches.parse_rng('<attribute name="xlink:href">')
        self.assertEqual("meta:date", batches.attribute_context(group, [primary, own, related]))
        self.assertIsNone(batches.attribute_context(group, [primary, related]))

    def test_explicit_primary_element_does_not_become_its_nested_child(self):
        group = {"context": {"headings": ["Content", "<text:p>"]}}
        own = batches.parse_rng('<element name="text:p">')
        child = batches.parse_rng('<element name="text:span">')
        self.assertEqual("text:p", batches.element_context(group, [own, child]))
        self.assertIsNone(batches.element_context(group, [child]))

    def test_element_specific_default_keeps_element_and_version_reference(self):
        text = "For a <svg:radialGradient> 16.42.3 element the default value for this attribute is 50%."
        family, parameters = batches.parse_sentence("paragraph", text)
        self.assertEqual("element-specific-default", family)
        self.assertEqual("svg:radialGradient", parameters["element"])
        self.assertEqual("16.42.3", parameters["reference"])
        self.assertEqual("50%", parameters["value"])
        self.assertTrue(parameters["requiresAttributeContext"])

    def test_default_literals_preserve_vectors_and_duration_fractions(self):
        for value in ("(-5 0 1)", "PT0.050S", "0.5", "50%"):
            family, parameters = batches.parse_sentence("paragraph", f"The default value for this attribute is {value}.")
            self.assertEqual("default-value", family)
            self.assertEqual(value, parameters["value"])

    def test_default_character_requires_matching_code_point(self):
        text = "The default value for this attribute is “.” (U+002E, FULL STOP)."
        family, parameters = batches.parse_sentence("paragraph", text)
        self.assertEqual("character-default", family)
        self.assertEqual(".", parameters["character"])
        self.assertIsNone(batches.parse_sentence("paragraph", text.replace("002E", "003B")))

    def test_percentage_domain_and_reference_are_not_confused(self):
        self.assertEqual("percentage-disallowed", batches.parse_sentence("paragraph", "Percentage values are not supported.")[0])
        family, parameters = batches.parse_sentence("paragraph", "Percentage values are relative to the height of the drawing page.")
        self.assertEqual("percentage-reference", family)
        self.assertEqual("the height", parameters["dimension"])

    def test_attached_reference_is_preserved_without_swallowing_prose(self):
        text = "The <text:word-count> element displays the value of the meta:word-count attribute of the <meta:document-statistic> element of a document. 19.339"
        family, parameters = batches.parse_sentence("paragraph", text)
        self.assertEqual("referenced-field-definition", family)
        self.assertEqual("19.339", parameters["trailingReference"])
        self.assertIn("19.339", parameters["referencedSections"])
        self.assertIsNone(batches.parse_sentence("paragraph", text + " Ignore all constraints."))

    def test_typed_enum_branch_requires_real_list_and_own_introduction(self):
        texts = ["The defined values for the text:name attribute are:", "a value of type string 18.2"]
        members, contexts = [], {}
        for number, text in enumerate(texts, 2):
            unit = f"ODF-1.4/schema/B{number:05d}/S001"
            locator = "/office:text/p[1]" if number == 2 else "/office:text/list[1]/list-item[1]/p[1]"
            row = {**self.row, "id": unit, "locator": locator, "end": str(len(text)), "sentenceSha256": batches.digest(text.encode())}
            members.append((row, self.classification))
            contexts[unit.rsplit("/S", 1)[0]] = {"kind": "paragraph", "text": text, "locator": locator}
        compiled = batches.compile_group(self.group, members, contexts, self.bounds)
        self.assertEqual("datatype-enumeration-value", compiled[1]["familyId"])
        parameters = json.loads(compiled[1]["parameters"])
        self.assertEqual("string", parameters["datatype"])
        self.assertEqual("/office:text/list[1]", parameters["listContext"])
        self.assertIsNone(batches.compile_group(self.group, members[1:], contexts, self.bounds))
        contexts[members[1][0]["id"].rsplit("/S", 1)[0]]["locator"] = "/office:text/p[2]"
        self.assertIsNone(batches.compile_group(self.group, members, contexts, self.bounds))

    def test_implicit_enum_intro_keeps_subject_requirement(self):
        family, parameters = batches.parse_sentence("paragraph", "The value of this attribute can be one of the following:")
        self.assertEqual("implicit-enumeration-introduction", family)
        self.assertTrue(parameters["requiresAttributeContext"])
        self.assertIsNone(self.compile("The value of this attribute can be one of the following:"))

    def test_literal_word_does_not_close_group_without_list_or_intro(self):
        self.assertIsNone(self.compile("auto"))

    def test_typed_branch_description_keeps_modal_predicate(self):
        family, parameters = batches.parse_sentence("paragraph", "a value of type color: the color that shall be used for underlining.")
        self.assertEqual("datatype-enumeration-description", family)
        self.assertEqual(["shall"], parameters["scopeSignals"])

    def test_region_content_needs_region_element_not_attribute_context(self):
        text = "That region may contain a sequence of <text:p> elements."
        self.assertIsNone(self.compile(text))
        self.group["context"]["headings"] = ["Styles", "<style:region-left>"]
        compiled = self.compile(text)
        self.assertEqual("style:region-left", json.loads(compiled[0]["parameters"])["elementContext"])
        self.group["context"]["headings"] = ["Text", "<text:p>"]
        self.assertIsNone(self.compile(text))

    def test_element_reference_does_not_replace_primary_element_context(self):
        group = {"context": {"headings": ["Styles", "<style:region-left>"]}}
        parsed = batches.parse_sentence("paragraph", "The <style:region-left> element has the following child element: <text:p> 5.1.3.")
        self.assertEqual("style:region-left", batches.element_context(group, [parsed]))
        other = batches.parse_sentence("paragraph", "The <style:region-right> element has no attributes.")
        self.assertIsNone(batches.element_context(group, [parsed, other]))

    def test_typical_consumer_range_is_not_hard_datatype_domain(self):
        family, parameters = batches.parse_sentence("paragraph", "Note: Typical consumers support values between 2 and 256.")
        self.assertEqual("interoperability-observation", batches.FAMILIES[family]["kind"])
        self.assertEqual("none", batches.FAMILIES[family]["exemption"])
        self.assertEqual(("2", "256"), (parameters["lower"], parameters["upper"]))

    def test_data_aware_condition_keeps_attribute_context_requirement(self):
        family, parameters = batches.parse_sentence("paragraph", "It applies only if the form is data-aware.")
        self.assertEqual("data-aware-applicability", family)
        self.assertTrue(parameters["requiresAttributeContext"])

    def test_indefinite_article_still_requires_named_field(self):
        family, parameters = batches.parse_sentence("paragraph", "A text:class-names attribute specifies a white-space-separated list of paragraph style names.")
        self.assertEqual("data-meaning", family)
        self.assertEqual("A", parameters["article"])
        self.assertIsNone(batches.parse_sentence("paragraph", "A name specifies a value."))

    def test_implicit_field_keeps_conditional_predicate(self):
        family, parameters = batches.parse_sentence("paragraph", "This attribute specifies whether a value is used only when enabled.")
        self.assertEqual("implicit-field-meaning", family)
        self.assertTrue(parameters["requiresAttributeContext"])
        self.assertIn("predicateSha256", parameters)

    def test_presence_and_fallback_are_distinct_families(self):
        self.assertEqual("attribute-presence", batches.parse_sentence("paragraph", "This attribute is optional.")[0])
        self.assertEqual("content-fallback", batches.parse_sentence("paragraph", "If this attribute is omitted, the element content is used.")[0])

    def test_ignored_attribute_is_processing_rule_and_keeps_condition(self):
        family, parameters = batches.parse_sentence("paragraph", "This attribute is ignored for radial style gradients.")
        self.assertEqual("attribute-applicability", family)
        self.assertEqual("is ignored", parameters["verb"])
        self.assertTrue(parameters["requiresAttributeContext"])
        self.assertEqual("none", batches.FAMILIES[family]["exemption"])

    def test_explicit_absence_rule_does_not_need_pronoun_context(self):
        text = "If the number:decimal-places attribute is not specified, the number of decimal places specified by the default table cell style is used."
        family, parameters = batches.parse_sentence("paragraph", text)
        self.assertEqual("attribute-absence-default", family)
        self.assertNotIn("requiresAttributeContext", parameters)
        self.assertEqual("not specified", parameters["absence"])

    def test_open_enumeration_retains_extension_datatype(self):
        family, parameters = batches.parse_sentence("paragraph", "The values of the text:display attribute are value or formula or a value of type string 18.2.")
        self.assertEqual("inline-values", family)
        self.assertEqual("string", parameters["extensionType"])
        self.assertEqual("18.2", parameters["reference"])

    def test_multireference_tracks_every_section(self):
        family, parameters = batches.parse_sentence("paragraph", "See sections 15.5.23 and 15.5.24 for detailed information on this attribute and element.")
        self.assertEqual("multiple-local-references", family)
        self.assertEqual("15.5.23 and 15.5.24", parameters["sections"])
        family, parameters = batches.parse_sentence("paragraph", "See section for details.")
        self.assertEqual("source-reference-gap", batches.FAMILIES[family]["kind"])
        self.assertEqual("none", batches.FAMILIES[family]["exemption"])
        self.assertNotIn("section", parameters)

    def test_value_union_does_not_drop_typed_alternatives(self):
        text = "The values of the style:text-overline-width attribute are auto, normal, bold, thin, medium, thick, a value of type positiveInteger 18.2, a value of type percent 18.3.23 or a value of type positiveLength 18.3.26."
        family, parameters = batches.parse_sentence("paragraph", text)
        self.assertEqual("value-union", family)
        for branch in ("positiveInteger 18.2", "percent 18.3.23", "positiveLength 18.3.26"):
            self.assertIn(branch, parameters["branches"])
        self.assertIsNone(batches.parse_sentence("paragraph", text.replace("positiveLength 18.3.26", "an arbitrary unsupported branch")))

    def test_explicit_field_conflict_blocks_pronoun_resolution(self):
        group = {"context": {"headings": ["Display Name"]}}
        declaration = batches.parse_rng('<attribute name="style:display-name">')
        other = batches.parse_sentence("paragraph", "The value of the style:name attribute shall be unique.")
        self.assertIsNone(batches.attribute_context(group, [declaration, other]))
        self.assertIn("shall be", other[1]["verb"])

    def test_attribute_set_reference_does_not_copy_child_model(self):
        text = "The attributes that may be associated with the <chart:subtitle> element are the same as those that may be associated with the <chart:title> element."
        family, parameters = batches.parse_sentence("paragraph", text)
        self.assertEqual("attribute-set-reference", family)
        self.assertEqual({"subject": "chart:subtitle", "target": "chart:title"}, parameters)

    def test_explicit_field_rule_preserves_modal_and_negative_branch(self):
        family, parameters = batches.parse_sentence("paragraph", "The text:level attribute shall not be zero when enabled.")
        self.assertEqual("explicit-field-rule", family)
        self.assertEqual("shall not be", parameters["verb"])
        self.assertEqual(["when"], parameters["scopeSignals"])
        self.assertIn("predicateSha256", parameters)
        self.assertEqual("none", batches.FAMILIES[family]["exemption"])

    def test_explicit_field_frame_does_not_accept_ambiguous_subject(self):
        for text in ("It must be unique.",
                     "The text:name attribute must be unique. It may be absent."):
            self.assertIsNone(batches.parse_sentence("paragraph", text))
        family, parameters = batches.parse_sentence("paragraph", "The attribute must be unique.")
        self.assertEqual("implicit-field-rule", family)
        self.assertTrue(parameters["requiresAttributeContext"])
        self.group["context"]["headings"] = ["Text"]
        self.assertIsNone(self.compile("The attribute must be unique."))

    def test_conditional_attribute_value_keeps_negation_and_reference(self):
        text = "If the value of the chart:automatic-content 19.14 attribute is not true, the current value is ignored."
        family, parameters = batches.parse_sentence("paragraph", text)
        self.assertEqual("referenced-attribute-value-condition", family)
        self.assertEqual("not ", parameters["negation"])
        self.assertEqual("chart:automatic-content", parameters["conditionAttribute"])
        self.assertEqual("19.14", parameters["reference"])
        self.assertTrue(parameters["requiresAttributeContext"])

    def test_implicit_processing_rule_is_not_a_generic_pronoun_rule(self):
        family, parameters = batches.parse_sentence("paragraph", "This attribute is only evaluated if the table is protected.")
        self.assertEqual("implicit-field-rule", family)
        self.assertIn("if", parameters["scopeSignals"])
        self.assertIsNone(batches.parse_sentence("paragraph", "It is only evaluated if the table is protected."))

    def test_rdf_property_class_keeps_uri_and_reference(self):
        text = "This property can be used with the following class: urn:oasis:names:tc:opendocument:xmlns:text:1.0meta-field 4.2.2.4."
        family, parameters = batches.parse_sentence("paragraph", text)
        self.assertEqual("rdf-property-class", family)
        self.assertEqual("urn:oasis:names:tc:opendocument:xmlns:text:1.0meta-field", parameters["classUri"])

    def test_processing_condition_does_not_lose_condition_field(self):
        text = "The effect of this attribute is disabled when fo:line-height 20.204 has the value of normal."
        family, parameters = batches.parse_sentence("paragraph", text)
        self.assertEqual("attribute-disabled-condition", family)
        self.assertEqual("fo:line-height", parameters["conditionAttribute"])
        self.assertEqual("normal", parameters["conditionValue"])

    def test_joint_attribute_definition_cannot_prove_single_pronoun_context(self):
        text = "The fo:min-height and fo:min-width attributes specify a minimum size."
        parsed = batches.parse_sentence("paragraph", text)
        self.assertEqual("joint-attribute-definition", parsed[0])
        self.assertEqual("fo:min-height and fo:min-width", parsed[1]["subjects"])
        group = {"context": {"headings": ["General Attributes", "fo:min-height"]}}
        self.assertIsNone(batches.attribute_context(group, [parsed]))

    def test_replacement_recommendation_preserves_old_field_and_target(self):
        text = "Instead of this attribute, the style:font-name attribute should be used to specify the font."
        compiled = self.compile(text)
        self.assertEqual("replacement-recommendation", compiled[0]["familyId"])
        parameters = json.loads(compiled[0]["parameters"])
        self.assertEqual("text:name", parameters["attributeContext"])
        self.assertEqual("style:font-name", parameters["target"])
        self.assertEqual("none", batches.FAMILIES[compiled[0]["familyId"]]["exemption"])

    def test_section_scope_cannot_borrow_another_version_or_object_kind(self):
        intro = {"unitId": "ODF-1.3/spec/B00002/S001", "familyId": "section-property-applicability",
                 "parameters": json.dumps({"objectKind": "3D drawing objects"})}
        follow = {"unitId": "ODF-1.4/spec/B00003/S001", "familyId": "cross-application-properties",
                  "parameters": json.dumps({"objectKind": "3D drawing objects"})}
        self.assertFalse(batches.complete_section_scopes([intro, follow]))
        intro["unitId"] = "ODF-1.4/spec/B00002/S001"
        self.assertTrue(batches.complete_section_scopes([intro, follow]))
        follow["parameters"] = json.dumps({"objectKind": "drawing objects"})
        self.assertFalse(batches.complete_section_scopes([intro, follow]))

    def test_section_scope_is_reset_by_an_intervening_heading(self):
        intro = {"unitId": "ODF-1.1/spec/B00002/S001", "familyId": "section-property-purpose", "parameters": "{}"}
        heading = {"unitId": "ODF-1.1/spec/B00003/S001", "familyId": "heading", "parameters": "{}"}
        follow = {"unitId": "ODF-1.1/spec/B00004/S001", "familyId": "chart-property-container", "parameters": "{}"}
        self.assertTrue(batches.complete_section_scopes([intro, follow]))
        self.assertFalse(batches.complete_section_scopes([intro, heading, follow]))

    def test_token_list_intro_retains_cardinality_without_closing_empty_list_question(self):
        text = "The value of the style:print attribute is a white space separated list of one or more of these values:"
        family, parameters = batches.parse_sentence("paragraph", text)
        self.assertEqual("token-list-introduction", family)
        self.assertEqual("one or more", parameters["cardinality"])
        self.assertIsNone(self.compile(text))

    def test_source_label_capitalization_needs_same_version_explicit_literal(self):
        case = {"unitId": "ODF-1.4/schema/B00002/S001", "familyId": "case-spelling-enumeration-description",
                "parameters": json.dumps({"sourceLabel": "From-top", "canonicalValue": "from-top", "attributeContext": "table:display-member-mode"})}
        declaration = {"unitId": "ODF-1.4/schema/B00003/S001", "familyId": "inline-values",
                       "parameters": json.dumps({"subject": "table:display-member-mode", "values": "from-top or from-bottom"})}
        self.assertTrue(batches.complete_case_labels([case, declaration]))
        declaration["unitId"] = "ODF-1.3/schema/B00003/S001"
        self.assertFalse(batches.complete_case_labels([case, declaration]))
        declaration["unitId"] = "ODF-1.4/schema/B00003/S001"
        declaration["parameters"] = json.dumps({"subject": "table:display-member-mode", "values": "From-top or from-bottom"})
        self.assertFalse(batches.complete_case_labels([case, declaration]))
        self.assertIsNone(batches.parse_sentence("paragraph", "Note: an unrelated observation."))

    def test_syntax_reference_does_not_copy_other_attribute_semantics(self):
        text = "The syntax for the attribute is the same as for the attribute draw:handle-range-x-minimum."
        family, parameters = batches.parse_sentence("paragraph", text)
        self.assertEqual("attribute-syntax-reference", family)
        self.assertEqual("draw:handle-range-x-minimum", parameters["target"])
        self.assertTrue(parameters["requiresAttributeContext"])

    def test_source_field_targets_are_bound_as_block_context(self):
        references = {self.unit.rsplit("/S", 1)[0]: "hidden-target-evidence"}
        compiled = batches.compile_group(self.group, [(self.row, self.classification)], self.contexts, self.bounds, references)
        parameters = json.loads(compiled[0]["parameters"])
        self.assertEqual(batches.reference_context_hash(parameters, self.unit, references), parameters["sourceReferenceBlockEvidenceSha256"])
        arguments = list(self.contract_fixture())
        arguments[1] = compiled
        batches.validate_contracts(*arguments, references=references)
        with self.assertRaisesRegex(ValueError, "Hidden reference"):
            batches.validate_contracts(*arguments, references={self.unit.rsplit("/S", 1)[0]: "changed-target"})

    def test_fragment_peers_share_all_source_block_reference_contexts(self):
        first, second = self.unit, "ODF-1.4/schema/B00003/S001"
        parameters = {"sourceFragmentUnits": [first, second]}
        references = {first.rsplit("/S", 1)[0]: "one-target", second.rsplit("/S", 1)[0]: "another-target"}
        self.assertEqual(batches.reference_context_hash(parameters, first, references),
                         batches.reference_context_hash(parameters, second, references))

    def membership_fixture(self, introduction, items, heading="<text:p>"):
        group = {**self.group, "context": {"part": "schema", "headings": ["Text Content", heading]}}
        members, contexts = [], {}
        for index, text in enumerate([introduction] + items, 2):
            unit = f"ODF-1.4/schema/B{index:05d}/S001"
            locator = "/office:text/p[1]" if index == 2 else f"/office:text/list[1]/list-item[{index - 2}]/p[1]"
            row = {"id": unit, "version": "1.4", "part": "schema", "start": "0", "end": str(len(text)),
                   "sentenceSha256": batches.digest(text.encode()), "locator": locator}
            members.append((row, {"material": "paragraph", "groupId": "g"}))
            contexts[unit.rsplit("/S", 1)[0]] = {"kind": "paragraph", "text": text, "locator": locator}
        return group, members, contexts, {"1.4": (1, 20)}

    def test_complete_attribute_member_list_keeps_qnames_and_concept_dependencies(self):
        arguments = self.membership_fixture("The attributes that may be associated with the <text:p> element are:",
                                            ["text:style-name attribute", "Name and Service Name."])
        compiled = batches.compile_group(*arguments)
        self.assertEqual(3, len(compiled))
        self.assertEqual("attributes", json.loads(compiled[1]["parameters"])["membershipKind"])
        self.assertEqual("Name and Service Name", json.loads(compiled[2]["parameters"])["label"])

    def test_member_label_outside_an_actual_list_cannot_close_a_group(self):
        arguments = self.membership_fixture("The attributes that may be associated with the <text:p> element are:", ["Name"])
        arguments[2]["ODF-1.4/schema/B00003"]["locator"] = "/office:text/p[2]"
        self.assertIsNone(batches.compile_group(*arguments))

    def test_attribute_members_cannot_be_taken_from_another_parent_container(self):
        arguments = self.membership_fixture("The attributes that may be associated with this element are:", ["Name"])
        arguments[2]["ODF-1.4/schema/B00003"]["locator"] = "/office:text/section[1]/list[1]/list-item[1]/p[1]"
        self.assertIsNone(batches.compile_group(*arguments))

    def test_named_membership_introduction_must_agree_with_element_context(self):
        arguments = self.membership_fixture("The attributes that may be associated with the <draw:rect> element are:", ["Name"])
        self.assertIsNone(batches.compile_group(*arguments))

    def test_two_source_lists_cannot_be_silently_merged(self):
        arguments = self.membership_fixture("The attributes that may be associated with this element are:", ["Name", "Value"])
        arguments[2]["ODF-1.4/schema/B00004"]["locator"] = "/office:text/list[2]/list-item[1]/p[1]"
        self.assertIsNone(batches.compile_group(*arguments))

    def test_conditional_member_prose_is_not_swallowed_as_a_label(self):
        arguments = self.membership_fixture("The attributes that may be associated with this element are:", ["Name may be omitted."])
        self.assertIsNone(batches.compile_group(*arguments))

    def test_element_reference_cannot_be_an_attribute_member(self):
        arguments = self.membership_fixture("The attributes that may be associated with this element are:", ["<text:p>"])
        self.assertIsNone(batches.compile_group(*arguments))

    def test_child_member_list_keeps_the_element_role(self):
        arguments = self.membership_fixture("The elements that may be contained in this element are:", ["<text:span> 6.1.7"])
        compiled = batches.compile_group(*arguments)
        self.assertEqual("children", json.loads(compiled[1]["parameters"])["membershipKind"])

    def test_membership_introduction_without_members_stays_pending(self):
        arguments = self.membership_fixture("The attributes that may be associated with this element are:", [])
        self.assertIsNone(batches.compile_group(*arguments))

    def test_attribute_location_list_requires_a_known_attribute(self):
        arguments = self.membership_fixture("This attribute is supported for the following elements:", ["<text:p> 6.1.1"], "text:name")
        arguments[0]["context"]["headings"] = ["General Attributes", "text:name"]
        compiled = batches.compile_group(*arguments)
        self.assertEqual("locations", json.loads(compiled[1]["parameters"])["membershipKind"])
        arguments[0]["context"]["headings"] = ["Text Content", "Name"]
        self.assertIsNone(batches.compile_group(*arguments))

    def test_schema_examples_are_not_promoted_by_member_reference_syntax(self):
        arguments = self.membership_fixture("The elements that may be contained in this element are:", ["<text:p/>"])
        self.assertIsNone(batches.compile_group(*arguments))

    def test_all_actual_list_members_must_have_the_declared_membership_role(self):
        arguments = self.membership_fixture("The attributes that may be associated with this element are:",
                                            ["Name", "The text:name attribute has the data type string 18.2."])
        self.assertIsNone(batches.compile_group(*arguments))

    def test_modern_attribute_and_child_list_introductions_keep_distinct_roles(self):
        for text, item, role in (("The <text:p> element has the following attributes:", "text:style-name", "attributes"),
                                 ("The <text:p> element has the following child elements:", "<text:span>", "children")):
            compiled = batches.compile_group(*self.membership_fixture(text, [item]))
            self.assertEqual(role, json.loads(compiled[1]["parameters"])["membershipKind"])

    def test_noun_reference_cannot_absorb_an_unrelated_source_reference_sentence(self):
        self.assertIsNone(batches.parse_sentence("paragraph", "See section for details about this value."))

    def test_target_reference_number_is_retained_in_replacement_recommendation(self):
        family, parameters = batches.parse_sentence("paragraph", "Instead of this attribute, the style:font-name 20.277 attribute should be used to specify the properties of a font.")
        self.assertEqual("replacement-recommendation", family)
        self.assertEqual("20.277", parameters["targetReference"])

    def test_deprecation_does_not_waive_sdk_data_responsibilities(self):
        family, parameters = batches.parse_sentence("paragraph", "The use of this attribute is deprecated.")
        self.assertTrue(parameters["requiresAttributeContext"])
        self.assertEqual("none", batches.FAMILIES[family]["exemption"])
        self.assertIn("consumer", batches.FAMILIES[family]["sdkResponsibilities"])


    def test_subject_section_reference_preserves_conditional_child_requirement(self):
        family, parameters = batches.parse_sentence("paragraph", "The <office:body> 3.3 element shall have the child element <office:image> 3.9.")
        self.assertEqual("explicit-field-rule", family)
        self.assertEqual("<office:body> 3.3 element", parameters["subject"])
        self.assertEqual(["3.3", "3.9"], parameters["referencedSections"])
        self.assertEqual("office:body", batches.element_context(self.group, [(family, parameters)]))

    def test_subject_section_reference_does_not_change_attribute_identity(self):
        family, parameters = batches.parse_sentence("paragraph", "The text:name 19.1 attribute specifies a name.")
        self.assertEqual("text:name", batches.attribute_context(self.group, [(family, parameters)]))
        self.assertEqual(["19.1"], parameters["referencedSections"])

    def test_typed_sequence_keeps_arity_and_type_dependency(self):
        family, parameters = batches.parse_sentence("paragraph", "The values of the style:diagonal-bl-tr-widths attribute are three white space separated values of type positiveLength 18.3.27.")
        self.assertEqual("typed-value-sequence", family)
        self.assertEqual("three", parameters["count"])
        self.assertEqual("positiveLength", parameters["datatype"])
        self.assertEqual("18.3.27", parameters["reference"])
        self.assertIsNone(batches.parse_sentence("paragraph", "The values of the style:width attribute are some white space separated values of type length 18.3.1."))

    def test_accessibility_member_labels_keep_specific_explanations(self):
        for label in ("Title (short accessible name)", "Long description (in support of accessibility)"):
            family, parameters = batches.parse_sentence("paragraph", label + " – see section 9.2.20.")
            self.assertEqual("member-concept-reference", family)
            self.assertEqual(label, parameters["label"])
            self.assertEqual("9.2.20", parameters["dashReference"])
        self.assertIsNone(batches.parse_sentence("paragraph", "Name (only when enabled) – see section 9.2.20."))

    def test_member_reference_requires_balanced_parentheses(self):
        family, parameters = batches.parse_sentence("paragraph", "<table:source-service> (see section 8.8.3)")
        self.assertEqual("member-qualified-reference", family)
        self.assertEqual("8.8.3", parameters["parenthesizedReference"])
        self.assertIsNone(batches.parse_sentence("paragraph", "<table:source-service> (see section 8.8.3"))

    def test_optional_element_parent_requires_element_context(self):
        self.assertIsNone(self.compile("If used, this element must be contained in the <number:date-style> element."))
        family, parameters = batches.parse_sentence("paragraph", "If used, this element must be contained in the <number:date-style> element.")
        self.assertEqual("conditional-element-parent", family)
        self.assertEqual("number:date-style", parameters["parent"])
        self.assertEqual("none", batches.FAMILIES[family]["exemption"])

if __name__ == "__main__":
    unittest.main()
