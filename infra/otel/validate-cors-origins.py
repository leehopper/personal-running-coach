#!/usr/bin/env python3
"""Pre-commit guard against `allowed_origins: ["*"]` in OTel collector config.

DEC-069 prohibits a wildcard `allowed_origins` on the OTLP/HTTP receiver
because the collector responds with `Access-Control-Allow-Credentials: true`,
which browsers refuse to combine with `Access-Control-Allow-Origin: *` per
the Fetch Standard. The constraint only applies to `allowed_origins` —
`allowed_headers: ["*"]` is a documented and intentional override (see the
DEC-069 amendment in `docs/decisions/decision-log.md`).
"""

from __future__ import annotations

import sys
from pathlib import Path

try:
    import yaml
except ImportError:
    sys.exit("validate-cors-origins.py requires PyYAML (pip install pyyaml).")


def main(path: str) -> int:
    p = Path(path)
    if not p.exists():
        return 0  # file removed in this commit — nothing to guard
    cfg = yaml.safe_load(p.read_text())
    origins = (
        cfg.get("receivers", {})
        .get("otlp", {})
        .get("protocols", {})
        .get("http", {})
        .get("cors", {})
        .get("allowed_origins")
        or []
    )
    if "*" in origins:
        sys.stderr.write(
            "ERROR: `allowed_origins` in `{p}` contains a bare \"*\" entry.\n"
            "DEC-069 prohibits this: the collector sets "
            "`Access-Control-Allow-Credentials: true` and browsers reject a "
            "wildcard origin per the CORS spec. List the explicit origin(s) "
            "instead.\n".format(p=p)
        )
        return 1
    return 0


if __name__ == "__main__":
    target = sys.argv[1] if len(sys.argv) > 1 else "infra/otel/otel-collector-config.yaml"
    raise SystemExit(main(target))
