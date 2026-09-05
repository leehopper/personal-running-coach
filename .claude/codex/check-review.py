#!/usr/bin/env python3
"""Apply semantic checks that the review JSON schema cannot express."""

import json
import sys


def rejection_reasons(report: dict) -> list[str]:
    """Return every semantic reason that rejects one review report."""
    reasons = []
    findings = report.get("findings", [])
    if report.get("gate") == "ACCEPT" and any(
        finding.get("severity") in {"blocker", "major"} for finding in findings
    ):
        reasons.append("gate is ACCEPT with a blocker or major finding")
    if not report.get("conformance"):
        reasons.append("conformance is empty")
    if not report.get("checks_run"):
        reasons.append("checks_run is empty")
    if not str(report.get("verdict", "")).strip():
        reasons.append("verdict is blank")
    if any(
        mutation.get("observed") == "NOT_RUNNABLE_IN_SANDBOX"
        for mutation in report.get("mutations", [])
    ) and not report.get("orchestrator_runs"):
        reasons.append("NOT_RUNNABLE_IN_SANDBOX mutation has no orchestrator_runs")
    return reasons


def main() -> int:
    """Load each report, print its semantic result, and return the process code."""
    rejected = False
    for path in sys.argv[1:]:
        with open(path, encoding="utf-8") as stream:
            reasons = rejection_reasons(json.load(stream))
        if reasons:
            rejected = True
            print(f"{path}: REJECT")
            for reason in reasons:
                print(f"  {reason}")
        else:
            print(f"{path}: OK")
    return 1 if rejected else 0


if __name__ == "__main__":
    sys.exit(main())
