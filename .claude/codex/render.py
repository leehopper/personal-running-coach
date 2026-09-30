#!/usr/bin/env python3
"""Render a prompt template with str.format placeholders from a JSON vars file.

Usage: python3 .claude/codex/render.py <template.txt> <vars.json> > prompt.txt

Same `{name}` convention the fleet driver applies to --template: a literal brace
is doubled (`{{` / `}}`). Unlike the fleet driver, which leaves an unfilled
`{name}` in the prompt, a missing placeholder here is an error.
"""

import json
import sys


def main() -> int:
    """Render argv[1] with the JSON object in argv[2] to stdout; return the exit code."""
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
