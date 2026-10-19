"""Mutation regressions for premature or stale implementation verdicts."""

import copy
import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("implementation", Path(__file__).resolve().parents[1] / "Test-OdfImplementationReviews.py")
implementation = importlib.util.module_from_spec(spec)
spec.loader.exec_module(implementation)


class ImplementationReviews(unittest.TestCase):
    def setUp(self):
        scratch = Path(__file__).resolve().parents[2] / "artifacts/odf-regression"
        scratch.mkdir(parents=True, exist_ok=True)
        self.directory = tempfile.TemporaryDirectory(dir=scratch)
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.groups = {"g": {"requiresFollowUp": True, "memberSha256": "source", "versions": ["1.4"]},
                       "dependency": {"requiresFollowUp": True, "memberSha256": "other"}}
        self.members = {"g": {"u"}, "dependency": {"v"}}
        self.semantic = {"reviews": [{"groupId": "g", "rules": [{"exemption": "none", "units": ["u"]}]}]}
        files = []
        for name, kind, symbol in (("code.cs", "implementation", "Evaluate"), ("test.cs", "regression", "Regression")):
            path = self.root / name
            path.write_text(symbol, encoding="utf-8")
            files.append({"path": name, "kind": kind, "symbols": [symbol], "sha256": implementation.digest(path.read_bytes())})
        self.review = {"groupId": "g", "memberSha256": "source", "versions": ["1.4"],
                       "semanticReviewSha256": implementation.record_hash(self.semantic["reviews"][0]),
                       "verdict": "verified-direct-obligations", "conditions": "Valid input type", "assessment": "Return value preserved",
                       "dependencies": [{"groupId": "dependency", "memberSha256": "other"}], "sourceFiles": files,
                       "decisions": [{"semanticRuleIndex": 0, "units": ["u"], "verdict": "implemented", "assessment": "Code and regression"}],
                       "testRuns": [{"framework": framework, "sourceEvidenceSha256": implementation.record_hash(files),
                                     "command": "dotnet test", "passed": 1, "failed": 0, "skipped": 0, "reportSha256": "a" * 64}
                                    for framework in ("net10.0", "net8.0")]}
        self.document = {"schemaVersion": 1, "fullConformanceClaim": False, "reviews": [self.review]}

    def validate(self, document=None):
        return implementation.validate(document or self.document, self.groups, self.members, self.semantic, self.root)

    def compiled_fixture(self):
        metadata = {key: "locked" for key in ("builderSha256", "familySha256", "sourceArchiveSha256",
                    "sourceReferenceEvidenceSha256", "bodyRangesSha256", "effectiveTextEvidenceSha256", "scopeEvidenceSha256")}
        metadata.update(groups=[{"groupId": "g", "memberSha256": "source", "versions": ["1.4"]}],
                        families={"definition": {"kind": "definition", "sdkResponsibilities": ["validator"],
                                                 "exemption": "none", "assessment": "Check type"}})
        bindings = [{"groupId": "g", "unitId": "u", "familyId": "definition", "parameters": "{type:string}"}]
        return metadata, bindings

    def test_compiled_source_can_receive_independent_implementation_verdict(self):
        metadata, bindings = self.compiled_fixture()
        self.semantic = implementation.with_family_sources({"reviews": []}, metadata, bindings)
        self.review["semanticReviewSha256"] = implementation.record_hash(self.semantic["reviews"][0])
        self.assertEqual(1, self.validate()["implementationPendingGroups"])
        self.assertEqual(1, self.validate()["implementationRuntimeVerifiedGroups"])

    def test_compiled_parameter_or_policy_change_invalidates_verdict(self):
        metadata, bindings = self.compiled_fixture()
        original = implementation.with_family_sources({"reviews": []}, metadata, bindings)
        self.review["semanticReviewSha256"] = implementation.record_hash(original["reviews"][0])
        for key in ("parameters", "policy"):
            changed, rows = copy.deepcopy(metadata), copy.deepcopy(bindings)
            if key == "parameters":
                rows[0]["parameters"] = "{type:boolean}"
            else:
                changed["familySha256"] = "changed"
            self.semantic = implementation.with_family_sources({"reviews": []}, changed, rows)
            with self.assertRaisesRegex(ValueError, "semantic interpretation changed"):
                self.validate()

    def test_compiled_and_manual_sources_cannot_double_count(self):
        metadata, bindings = self.compiled_fixture()
        with self.assertRaisesRegex(ValueError, "overlap"):
            implementation.with_family_sources(self.semantic, metadata, bindings)

    def test_compiled_source_requires_unique_complete_known_bindings(self):
        metadata, bindings = self.compiled_fixture()
        for rows in ([], bindings * 2, [{**bindings[0], "groupId": "foreign"}], [{**bindings[0], "familyId": "unknown"}]):
            with self.assertRaises(ValueError):
                implementation.with_family_sources({"reviews": []}, metadata, rows)

    def test_direct_verdict_does_not_close_referenced_group(self):
        result = self.validate()
        self.assertEqual(1, result["implementationAdjudicatedDirectGroups"])
        self.assertEqual(1, result["implementationPendingGroups"])
        self.assertEqual(1, result["implementationPendingDependencyGroups"])
        self.assertFalse(result["implementationAdjudicationComplete"])
        self.assertFalse(result["fullConformanceClaim"])

    def no_runtime(self, exemption="non-obligation"):
        self.semantic["reviews"][0]["rules"][0]["exemption"] = exemption
        self.review.update(verdict="no-sdk-runtime-obligations", sourceFiles=[], testRuns=[], dependencies=[],
                           semanticReviewSha256=implementation.record_hash(self.semantic["reviews"][0]))
        self.review["decisions"][0]["verdict"] = "outside-sdk" if exemption == "outside-sdk" else "source-context"

    def test_no_runtime_context_preserves_pending_dependency(self):
        for exemption in ("non-obligation", "outside-sdk"):
            self.no_runtime(exemption)
            result = self.validate()
            self.assertEqual(1, result["implementationPendingGroups"])
            self.assertEqual(0, result["implementationRuntimeVerifiedGroups"])
            self.assertEqual(1, result["implementationNoRuntimeGroups"])

    def test_no_runtime_cannot_hide_normative_rule(self):
        self.no_runtime("none")
        with self.assertRaisesRegex(ValueError, "Normative SDK rule"):
            self.validate()

    def test_no_runtime_cannot_imply_test_execution_or_dependencies(self):
        for key, value in (("testRuns", [{}]), ("sourceFiles", [{}]), ("dependencies", [{}])):
            self.no_runtime()
            self.review[key] = value
            with self.assertRaisesRegex(ValueError, "must not imply"):
                self.validate()

    def test_changed_semantic_interpretation_invalidates_implementation(self):
        self.semantic["reviews"][0]["rules"][0]["assessment"] = "Different requirement"
        with self.assertRaisesRegex(ValueError, "semantic interpretation changed"):
            self.validate()

    def test_changed_code_or_regression_requires_reverification(self):
        for path in ("code.cs", "test.cs"):
            with self.subTest(path=path):
                original = (self.root / path).read_bytes()
                (self.root / path).write_bytes(original + b" changed")
                with self.assertRaisesRegex(ValueError, "changed after verification"):
                    self.validate()
                (self.root / path).write_bytes(original)

    def test_missing_framework_or_failed_run_cannot_close_group(self):
        for mutation in (lambda r: r["testRuns"].pop(), lambda r: r["testRuns"][0].update(failed=1),
                         lambda r: r["testRuns"][0].update(skipped=1), lambda r: r["testRuns"][0].update(passed=0)):
            document = copy.deepcopy(self.document)
            mutation(document["reviews"][0])
            with self.assertRaises(ValueError):
                self.validate(document)

    def test_run_is_bound_to_exact_evidence_files(self):
        self.review["testRuns"][0]["sourceEvidenceSha256"] = "stale"
        with self.assertRaisesRegex(ValueError, "source-bound"):
            self.validate()

    def test_missing_or_duplicate_unit_cannot_close_group(self):
        for units in ([], ["u", "u"], ["foreign"]):
            document = copy.deepcopy(self.document)
            document["reviews"][0]["decisions"][0]["units"] = units
            with self.assertRaises(ValueError):
                self.validate(document)

    def test_normative_rule_cannot_be_waived_as_context(self):
        self.review["decisions"][0]["verdict"] = "source-context"
        with self.assertRaisesRegex(ValueError, "Normative SDK rule"):
            self.validate()

    def test_sdk_exclusion_is_not_implemented_behavior(self):
        self.semantic["reviews"][0]["rules"][0]["exemption"] = "outside-sdk"
        self.review["semanticReviewSha256"] = implementation.record_hash(self.semantic["reviews"][0])
        with self.assertRaisesRegex(ValueError, "exclusion was silently"):
            self.validate()
        self.review["decisions"][0]["verdict"] = "outside-sdk"
        self.validate()

    def test_dependency_decision_requires_known_explicit_target(self):
        decision = self.review["decisions"][0]
        decision.update(verdict="dependency", dependencyGroupIds=["missing"])
        with self.assertRaisesRegex(ValueError, "explicit pending dependencies"):
            self.validate()
        decision["dependencyGroupIds"] = ["dependency"]
        self.assertEqual(1, self.validate()["implementationPendingGroups"])

    def test_regression_evidence_cannot_be_replaced_by_code_only(self):
        self.review["sourceFiles"][1]["kind"] = "implementation"
        with self.assertRaisesRegex(ValueError, "code and regression"):
            self.validate()

    def test_full_conformance_claim_is_never_inferred(self):
        self.document["fullConformanceClaim"] = True
        with self.assertRaisesRegex(ValueError, "cannot claim full conformance"):
            self.validate()

    def local_receipt(self):
        receipt = self.root / "run.trx"
        content = ('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                   '<ResultSummary><Counters total="1" passed="1" failed="0" error="0" timeout="0" aborted="0" notExecuted="0"/></ResultSummary>'
                   '<Results><UnitTestResult testName="Regression" outcome="Passed"/></Results></TestRun>')
        receipt.write_text(content, encoding="utf-8")
        for run in self.review["testRuns"]:
            run.update(localReport="run.trx", reportSha256=implementation.digest(receipt.read_bytes()))
        return receipt, content

    def test_local_receipt_requires_matching_case_and_success_counts(self):
        receipt, content = self.local_receipt()
        implementation.check_local_receipts(self.document, self.root)
        receipt.write_text(content.replace('outcome="Passed"', 'outcome="Failed"'), encoding="utf-8")
        for run in self.review["testRuns"]:
            run["reportSha256"] = implementation.digest(receipt.read_bytes())
        with self.assertRaisesRegex(ValueError, "regression case is absent"):
            implementation.check_local_receipts(self.document, self.root)
        receipt.write_text(content, encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "changed local test receipt"):
            implementation.check_local_receipts(self.document, self.root)

    def test_shared_receipt_keeps_each_reviews_required_cases(self):
        self.local_receipt()
        other = copy.deepcopy(self.review)
        other["sourceFiles"][1]["symbols"] = ["OtherCase"]
        self.document["reviews"].append(other)
        with self.assertRaisesRegex(ValueError, "regression case is absent"):
            implementation.check_local_receipts(self.document, self.root)

    def test_shared_receipt_keeps_each_runs_expected_fingerprint(self):
        self.local_receipt()
        self.review["testRuns"][1]["reportSha256"] = "b" * 64
        with self.assertRaisesRegex(ValueError, "changed local test receipt"):
            implementation.check_local_receipts(self.document, self.root)

    def test_shared_receipt_keeps_each_runs_expected_counts(self):
        self.local_receipt()
        self.review["testRuns"][1]["passed"] = 2
        with self.assertRaisesRegex(ValueError, "count differs"):
            implementation.check_local_receipts(self.document, self.root)

    def test_changed_receipt_is_rejected_before_parsing_xml(self):
        receipt, _ = self.local_receipt()
        receipt.write_text("not XML", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "changed local test receipt"):
            implementation.check_local_receipts(self.document, self.root)


if __name__ == "__main__":
    unittest.main()
