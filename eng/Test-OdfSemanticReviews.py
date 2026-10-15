"""Check explicit group semantic reviews without treating them as conformance proof."""

import argparse
import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.dont_write_bytecode = True
KINDS = {"source-heading", "definition", "permission", "requirement", "recommendation",
         "conditional-permission-and-requirement", "historical-context", "informative-context"}
SCOPES = {"validator", "consumer", "producer", "source-context", "outside-sdk"}


def validate(document, groups, members, root=ROOT):
    if document.get("schemaVersion") != 1 or document.get("fullConformanceClaim") is not False:
        raise ValueError("Semantic review must not assert full conformance")
    seen = set()
    for review in document["reviews"]:
        identifier = review["groupId"]
        if identifier in seen or identifier not in groups or not groups[identifier]["requiresFollowUp"]:
            raise ValueError("Duplicate, unknown or inapplicable review group")
        seen.add(identifier)
        group = groups[identifier]
        if review["memberSha256"] != group["memberSha256"]:
            raise ValueError("Reviewed group source membership changed")
        if review["versions"] != group["versions"] or not review["versionAssessment"].strip():
            raise ValueError("Missing version assessment")
        if review["implementationAdjudication"] != "pending":
            raise ValueError("Semantic classification cannot close implementation adjudication")
        covered = set()
        for rule in review["rules"]:
            if rule["kind"] not in KINDS or not rule["sdkResponsibilities"] or not set(rule["sdkResponsibilities"]) <= SCOPES:
                raise ValueError("Unknown semantic role or SDK responsibility")
            if not rule["assessment"].strip() or not rule["conditions"].strip():
                raise ValueError("Missing semantic assessment or applicability conditions")
            if rule["exemption"] not in ("none", "non-obligation", "outside-sdk"):
                raise ValueError("Unknown exemption")
            if "source-context" in rule["sdkResponsibilities"] and (rule["kind"] not in ("source-heading", "historical-context", "informative-context")
                    or rule["exemption"] != "non-obligation"):
                raise ValueError("Normative semantics cannot be hidden in source context")
            if rule["exemption"] == "non-obligation" and (rule["kind"] not in ("source-heading", "historical-context", "informative-context")
                    or rule["sdkResponsibilities"] != ["source-context"]):
                raise ValueError("Normative semantics cannot be waived as source context")
            if rule["exemption"] == "outside-sdk" and (rule["sdkResponsibilities"] != ["outside-sdk"]
                    or not rule.get("scopeEvidence")):
                raise ValueError("SDK exclusion requires explicit scope evidence")
            if "outside-sdk" in rule["sdkResponsibilities"] and rule["exemption"] != "outside-sdk":
                raise ValueError("SDK exclusion must carry an exemption assessment")
            for reference in rule.get("scopeEvidence", []):
                path = (root / reference).resolve()
                if not path.is_relative_to(root.resolve()) or not path.is_file():
                    raise ValueError("Missing or unsafe scope evidence")
            if not rule["units"]:
                raise ValueError("Empty semantic rule")
            for unit in rule["units"]:
                if unit in covered or unit not in members[identifier]:
                    raise ValueError("Duplicate or foreign semantic member")
                covered.add(unit)
        if covered != members[identifier]:
            raise ValueError("Semantic review does not cover the whole group")
    total = sum(group["requiresFollowUp"] for group in groups.values())
    return {"followUpGroups": total, "semanticReviewedGroups": len(seen),
            "semanticPendingGroups": total - len(seen), "implementationPendingGroups": total,
            "semanticClassificationComplete": len(seen) == total, "fullConformanceClaim": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--require-complete", action="store_true")
    options = parser.parse_args()
    groups = {group["id"]: group for group in json.loads(
        (ROOT / "docs/odf-obligation-classification/groups.json").read_text(encoding="utf-8"))["groups"]}
    members = {identifier: set() for identifier in groups}
    for version in ("1.0", "1.1", "1.2", "1.3", "1.4"):
        with (ROOT / f"docs/odf-obligation-classification/odf-{version}.csv").open(encoding="utf-8", newline="") as stream:
            for row in csv.DictReader(stream):
                members[row["groupId"]].add(row["id"])
    document = json.loads((ROOT / "docs/odf-semantic-reviews.json").read_text(encoding="utf-8"))
    result = validate(document, groups, members)
    print(json.dumps(result))
    if options.require_complete and not result["semanticClassificationComplete"]:
        raise SystemExit("Semantic classification remains incomplete")


if __name__ == "__main__":
    main()
