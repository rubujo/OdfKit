"""Check source-bound implementation verdicts without inferring dependency coverage."""

import argparse
import csv
import hashlib
import importlib.util
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parent.parent
TARGET = "docs/odf-implementation-reviews.json"


def digest(value):
    return hashlib.sha256(value).hexdigest()


def record_hash(value):
    return digest(json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode())


def with_family_sources(document, family_document, bindings):
    reviews = list(document["reviews"])
    manual = {review["groupId"] for review in reviews}
    compiled = {group["groupId"]: group for group in family_document["groups"]}
    if manual & compiled.keys() or len(compiled) != len(family_document["groups"]):
        raise ValueError("Manual and compiled semantic sources overlap or repeat")
    rules = {identifier: [] for identifier in compiled}
    seen = set()
    for binding in bindings:
        identifier, unit = binding["groupId"], binding["unitId"]
        if identifier not in compiled or unit in seen or binding["familyId"] not in family_document["families"]:
            raise ValueError("Unknown or repeated compiled semantic binding")
        seen.add(unit)
        family = family_document["families"][binding["familyId"]]
        rules[identifier].append({"kind": family["kind"], "sdkResponsibilities": family["sdkResponsibilities"],
                                  "exemption": family["exemption"], "assessment": family["assessment"],
                                  "units": [unit], "sourceBinding": binding})
    basis = {key: family_document[key] for key in ("builderSha256", "familySha256", "sourceArchiveSha256",
             "sourceReferenceEvidenceSha256", "bodyRangesSha256", "effectiveTextEvidenceSha256", "scopeEvidenceSha256")}
    for identifier, group in compiled.items():
        if not rules[identifier]:
            raise ValueError("Compiled semantic group has no source bindings")
        reviews.append({**group, "compiledEvidence": basis, "rules": rules[identifier]})
    return {**document, "reviews": reviews}


def load_family_sources(document, root=ROOT):
    with (root / "docs/odf-semantic-batches/bindings.csv").open(encoding="utf-8", newline="") as stream:
        return with_family_sources(document, json.loads(
            (root / "docs/odf-semantic-batches/groups.json").read_text(encoding="utf-8")), list(csv.DictReader(stream)))


def check_local_receipts(document, root=ROOT):
    namespace = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
    receipts = {}
    for review in document["reviews"]:
        cases = {symbol for item in review["sourceFiles"] if item["kind"] == "regression" for symbol in item["symbols"]}
        for run in review["testRuns"]:
            path = (root / run["localReport"]).resolve()
            if not path.is_relative_to(root.resolve()) or not path.is_file():
                raise ValueError("Missing, unsafe, or changed local test receipt")
            if path not in receipts:
                content = path.read_bytes()
                if digest(content) != run["reportSha256"]:
                    raise ValueError("Missing, unsafe, or changed local test receipt")
                report = ET.fromstring(content)
                counters = report.find(".//" + namespace + "Counters")
                passed_cases = tuple(result.get("testName", "") for result in report.findall(".//" + namespace + "UnitTestResult")
                                     if result.get("outcome") == "Passed")
                receipts[path] = (digest(content), counters, passed_cases)
            fingerprint, counters, passed_cases = receipts[path]
            if fingerprint != run["reportSha256"]:
                raise ValueError("Missing, unsafe, or changed local test receipt")
            if counters is None or any(int(counters.get(name, "-1")) != 0 for name in ("failed", "error", "timeout", "aborted", "notExecuted")):
                raise ValueError("Local test receipt contains unsuccessful or skipped results")
            if int(counters.get("passed", "-1")) != run["passed"] or int(counters.get("total", "-1")) != run["passed"]:
                raise ValueError("Local test receipt count differs from recorded run")
            if any(not any(case in name for name in passed_cases) for case in cases):
                raise ValueError("Reviewed regression case is absent from successful local receipt")


def validate(document, groups, members, semantic_document, root=ROOT):
    if document.get("schemaVersion") != 1 or document.get("fullConformanceClaim") is not False:
        raise ValueError("Implementation review cannot claim full conformance")
    semantic = {review["groupId"]: review for review in semantic_document["reviews"]}
    seen = set()
    for review in document["reviews"]:
        identifier = review["groupId"]
        if identifier in seen or identifier not in groups or identifier not in semantic or not groups[identifier]["requiresFollowUp"]:
            raise ValueError("Duplicate, unknown, or unclassified implementation group")
        seen.add(identifier)
        group, source = groups[identifier], semantic[identifier]
        if review["memberSha256"] != group["memberSha256"] or review["semanticReviewSha256"] != record_hash(source):
            raise ValueError("Implementation source or semantic interpretation changed")
        if review["versions"] != group["versions"] or review["verdict"] not in ("verified-direct-obligations", "no-sdk-runtime-obligations"):
            raise ValueError("Unknown implementation version or verdict")
        if not review["conditions"].strip() or not review["assessment"].strip():
            raise ValueError("Missing implementation scope or assessment")
        no_runtime = review["verdict"] == "no-sdk-runtime-obligations"
        if no_runtime and any(rule["exemption"] not in ("non-obligation", "outside-sdk") for rule in source["rules"]):
            raise ValueError("Normative SDK rule cannot be closed without runtime evidence")
        if no_runtime and (review["sourceFiles"] or review["testRuns"] or review["dependencies"]):
            raise ValueError("No-runtime verdict must not imply tested code or close dependencies")
        dependencies = review["dependencies"]
        if len({item["groupId"] for item in dependencies}) != len(dependencies):
            raise ValueError("Duplicate implementation dependency")
        for item in dependencies:
            target = item["groupId"]
            if target == identifier or target not in groups or item["memberSha256"] != groups[target]["memberSha256"]:
                raise ValueError("Unknown or changed implementation dependency")
        evidence = review["sourceFiles"]
        paths, kinds = set(), set()
        for item in evidence:
            path = (root / item["path"]).resolve()
            if item["path"] in paths or not path.is_relative_to(root.resolve()) or not path.is_file():
                raise ValueError("Duplicate, missing, or unsafe implementation evidence")
            paths.add(item["path"])
            if item["kind"] not in ("implementation", "regression"):
                raise ValueError("Unknown implementation evidence role")
            kinds.add(item["kind"])
            content = path.read_bytes().replace(b"\r\n", b"\n")
            if digest(content) != item["sha256"]:
                raise ValueError("Implementation or regression changed after verification")
            if not item["symbols"] or any(not symbol or symbol not in content.decode("utf-8-sig") for symbol in item["symbols"]):
                raise ValueError("Missing reviewed implementation or regression symbol")
        if not no_runtime and kinds != {"implementation", "regression"}:
            raise ValueError("Implementation verdict requires code and regression evidence")
        runs, frameworks = review["testRuns"], set()
        for run in runs:
            if run["framework"] in frameworks or run["framework"] not in ("net10.0", "net8.0"):
                raise ValueError("Duplicate or unknown tested framework")
            frameworks.add(run["framework"])
            if (run["sourceEvidenceSha256"] != record_hash(evidence) or not run["command"].strip()
                    or type(run["passed"]) is not int or run["passed"] < 1 or run["failed"] != 0 or run["skipped"] != 0
                    or not re.fullmatch(r"[0-9a-f]{64}", run["reportSha256"])):
                raise ValueError("Missing successful source-bound test execution")
        if not no_runtime and frameworks != {"net10.0", "net8.0"}:
            raise ValueError("Missing affected test framework")
        covered, rule_indices = set(), set()
        for decision in review["decisions"]:
            index = decision["semanticRuleIndex"]
            if type(index) is not int or index in rule_indices or not 0 <= index < len(source["rules"]):
                raise ValueError("Duplicate or unknown semantic rule decision")
            rule_indices.add(index)
            rule = source["rules"][index]
            if set(decision["units"]) != set(rule["units"]) or len(decision["units"]) != len(set(decision["units"])):
                raise ValueError("Implementation decision changed semantic rule members")
            if not decision["assessment"].strip():
                raise ValueError("Missing rule implementation assessment")
            verdict = decision["verdict"]
            if rule["exemption"] == "outside-sdk":
                if verdict != "outside-sdk":
                    raise ValueError("SDK exclusion was silently treated as implemented")
            elif rule["exemption"] == "non-obligation":
                if verdict != "source-context":
                    raise ValueError("Source context was silently treated as implemented")
            elif verdict == "dependency":
                targets = decision.get("dependencyGroupIds", [])
                if not targets or not set(targets) <= {item["groupId"] for item in dependencies}:
                    raise ValueError("Reference decision requires explicit pending dependencies")
            elif verdict != "implemented":
                raise ValueError("Normative SDK rule lacks an implementation verdict")
            for unit in decision["units"]:
                if unit in covered or unit not in members[identifier]:
                    raise ValueError("Duplicate or foreign implementation unit")
                covered.add(unit)
        if covered != members[identifier] or rule_indices != set(range(len(source["rules"]))):
            raise ValueError("Implementation review does not adjudicate every group member")
    total = sum(group["requiresFollowUp"] for group in groups.values())
    dependencies = {item["groupId"] for review in document["reviews"] for item in review["dependencies"]}
    return {"implementationAdjudicatedDirectGroups": len(seen), "implementationPendingGroups": total - len(seen),
            "implementationRuntimeVerifiedGroups": sum(review["verdict"] == "verified-direct-obligations" for review in document["reviews"]),
            "implementationNoRuntimeGroups": sum(review["verdict"] == "no-sdk-runtime-obligations" for review in document["reviews"]),
            "implementationPendingDependencyGroups": len(dependencies - seen),
            "implementationAdjudicationComplete": total == len(seen), "fullConformanceClaim": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--require-complete", action="store_true")
    parser.add_argument("--check-local-receipts", action="store_true")
    options = parser.parse_args()
    spec = importlib.util.spec_from_file_location("semantic_batches", ROOT / "eng/Build-OdfSemanticBatches.py")
    batches = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(batches)
    groups, rows, classified = batches.load_inventory(ROOT)
    members = {identifier: set() for identifier in groups}
    for unit in rows:
        members[classified[unit]["groupId"]].add(unit)
    document = json.loads((ROOT / TARGET).read_text(encoding="utf-8"))
    batches.check_contracts(ROOT)
    semantics = load_family_sources(json.loads((ROOT / "docs/odf-semantic-reviews.json").read_text(encoding="utf-8")))
    result = validate(document, groups, members, semantics)
    if options.check_local_receipts:
        check_local_receipts(document)
    print(json.dumps(result))
    if options.require_complete and not result["implementationAdjudicationComplete"]:
        raise SystemExit("Implementation adjudication remains incomplete")


if __name__ == "__main__":
    main()
