#!/usr/bin/env python3
"""Render a prompt template with str.format placeholders from a JSON vars file.

Usage: python3 .claude/codex/render.py <template.txt> <vars.json> > prompt.txt

Same rendering the fleet driver applies to --template, so one convention holds
for fleet templates and companion briefs alike: `{name}` is a placeholder and a
literal brace is doubled (`{{` / `}}`). A missing placeholder is an error, not an
empty string.
"""

import json
import sys


def main() -> int:
    if len(sys.argv) != 3:
        print(__doc__, file=sys.stderr)
        return 2
    template = open(sys.argv[1], encoding="utf-8").read()
    variables = json.load(open(sys.argv[2], encoding="utf-8"))
    try:
        sys.stdout.write(template.format(**variables))
    except KeyError as missing:
        print(f"missing placeholder value: {missing}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
