#!/usr/bin/env python3
"""Validate a context7.json against the constraints Context7 enforces.

Context7 drops a config that fails its schema *silently* — nothing surfaces in the
repository, and the library still indexes, just without the settings applied. The
failure is only visible in Context7's own indexing log, which nobody reads on a
normal day. This turns that into a build failure instead.

Constraints are transcribed from https://context7.com/schema/context7.json rather
than fetched, so the check stays offline and deterministic. Re-check them if that
schema changes.

Deliberately stdlib-only: it has to run on any runner, in any of the SDK repos,
without installing anything.

Usage:
    validate-context7.py [PATH]        # defaults to ./context7.json

Exit codes:
    0  valid, or no file present (nothing to check)
    1  invalid — one annotated error per problem
    2  the file could not be read or parsed
"""

from __future__ import annotations

import json
import os
import sys

# --- transcribed from the published schema ------------------------------------
MAX_LEN = {
    "projectTitle": 100,
    "description": 200,
    "branch": 100,
    "redirect": 500,
    "url": 500,
    "public_key": 100,
}
MAX_ITEM_LEN = {
    "folders": 255,
    "excludeFolders": 255,
    "excludeFiles": 255,
    "rules": 255,
    "previousVersions": 50,
}
MAX_ITEMS = {
    "folders": 50,
    "excludeFolders": 50,
    "excludeFiles": 100,
    "rules": 50,
    "previousVersions": 20,
}
MIN_LEN = {"description": 10, "projectTitle": 1}
KNOWN = {
    "$schema", "projectTitle", "description", "branch", "folders", "excludeFolders",
    "excludeFiles", "rules", "disallow", "redirect", "previousVersions", "url",
    "public_key",
}
# excludeFiles entries are filenames, never paths.
PATH_SEPARATORS = ("/", "\\")
# ------------------------------------------------------------------------------

IN_ACTIONS = os.environ.get("GITHUB_ACTIONS") == "true"


def annotate(path: str, message: str) -> None:
    """Emit a GitHub file annotation in CI, a plain line elsewhere."""
    print(f"::error file={path}::{message}" if IN_ACTIONS else f"error: {message}")


def validate(cfg: object) -> list[str]:
    errors: list[str] = []

    if not isinstance(cfg, dict):
        return ["top level must be a JSON object"]

    for key in sorted(set(cfg) - KNOWN):
        errors.append(f"unknown property '{key}' — the schema sets additionalProperties: false")

    for key, cap in MAX_LEN.items():
        value = cfg.get(key)
        if isinstance(value, str) and len(value) > cap:
            errors.append(f"{key} is {len(value)} characters, limit {cap}")

    for key, floor in MIN_LEN.items():
        value = cfg.get(key)
        if isinstance(value, str) and len(value) < floor:
            errors.append(f"{key} is {len(value)} characters, minimum {floor}")

    for key, cap in MAX_ITEMS.items():
        value = cfg.get(key)
        if isinstance(value, list) and len(value) > cap:
            errors.append(f"{key} has {len(value)} entries, limit {cap}")

    for key, cap in MAX_ITEM_LEN.items():
        for index, item in enumerate(cfg.get(key, []) or [], start=1):
            if isinstance(item, str) and len(item) > cap:
                preview = item[:60].replace("\n", " ")
                errors.append(
                    f"{key}[{index}] is {len(item)} characters, limit {cap}: {preview}..."
                )

    for index, name in enumerate(cfg.get("excludeFiles", []) or [], start=1):
        if isinstance(name, str) and any(sep in name for sep in PATH_SEPARATORS):
            errors.append(
                f"excludeFiles[{index}] '{name}' contains a path separator — "
                "entries must be bare filenames"
            )

    for key in ("folders", "excludeFolders", "excludeFiles", "rules"):
        value = cfg.get(key)
        if isinstance(value, list) and len(value) != len(set(map(str, value))):
            errors.append(f"{key} contains duplicate entries")

    if ("url" in cfg) != ("public_key" in cfg):
        errors.append("url and public_key must be provided together, or not at all")

    if cfg.get("disallow") is True and len(set(cfg) - {"disallow", "$schema"}) > 0:
        errors.append("disallow: true prevents parsing, so the other settings do nothing")

    return errors


def main(argv: list[str]) -> int:
    path = argv[1] if len(argv) > 1 else "context7.json"

    if not os.path.exists(path):
        print(f"No {path} in this repository; nothing to validate.")
        return 0

    try:
        with open(path, encoding="utf-8") as handle:
            cfg = json.load(handle)
    except json.JSONDecodeError as error:
        annotate(path, f"invalid JSON: {error}")
        return 2
    except OSError as error:
        print(f"error: could not read {path}: {error}")
        return 2

    errors = validate(cfg)
    if errors:
        for error in errors:
            annotate(path, error)
        print(f"\n{path}: {len(errors)} problem(s). Context7 would ignore this config.")
        return 1

    rules = cfg.get("rules", []) or []
    longest = max((len(r) for r in rules), default=0)
    print(
        f"{path} is valid: {len(rules)} rule(s), longest {longest} characters "
        f"(limit {MAX_ITEM_LEN['rules']})."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
