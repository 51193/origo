#!/usr/bin/env python3
"""Rewrite the Directory.Build.props version stamps for a release or snapshot.

Both the release and weekly workflows call this helper instead of embedding the
same rewriting logic twice. The caller keeps the tag validation and the
after-sync output group.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

PROPS = Path("Directory.Build.props")


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print("ERROR: expected exactly one version argument.", file=sys.stderr)
        return 1

    version = argv[1]
    # Tags are exactly x.y.z or x.y.z-<suffix>. Four-part/truncated versions
    # used to pass here and produce an AssemblyVersion that could never match
    # the release verifier, so reject the shape before touching the file.
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?", version):
        print(f"ERROR: version '{version}' must be x.y.z or x.y.z-<suffix>.", file=sys.stderr)
        return 1
    numeric = version.split("-", 1)[0]
    parts = numeric.split(".")
    assembly_version = ".".join(parts + ["0"])

    if not PROPS.is_file():
        print(f"ERROR: {PROPS} is missing.", file=sys.stderr)
        return 1

    content = PROPS.read_text(encoding="utf-8")
    for element, replacement in (
        ("Version", version),
        ("AssemblyVersion", assembly_version),
        ("FileVersion", assembly_version),
    ):
        content, count = re.subn(
            rf"(<{element}>)[^<]+(</{element}>)",
            lambda match, value=replacement: f"{match.group(1)}{value}{match.group(2)}",
            content,
        )
        if count != 1:
            print(
                f"ERROR: expected exactly one <{element}> element in {PROPS}, found {count}.",
                file=sys.stderr,
            )
            return 1

    PROPS.write_text(content, encoding="utf-8")
    print(
        f"Synced {PROPS}: <Version>={version}, "
        f"<AssemblyVersion>=<FileVersion>={assembly_version}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
