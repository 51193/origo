#!/usr/bin/env python3
"""Lint the pull-request title and body that GitHub uses for squash merges.

The PR title becomes the squash commit subject and the PR body becomes its
body. Local ``lint-commits.sh`` cannot see that generated commit, so the same
subject and body rules are checked on the PR event instead. Fenced code blocks,
Markdown table rows, and single-URL lines are exempt from the body-line rule
because they are not prose that can be wrapped.
"""

from __future__ import annotations

import json
import os
import re
import sys
from pathlib import Path

_CONVENTIONAL_SUBJECT = re.compile(
    r"^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)"
    r"(\([^)]*\))?!?: .+"
)


def main() -> int:
    event_path = os.environ.get("GITHUB_EVENT_PATH")
    if not event_path:
        print("ERROR: GITHUB_EVENT_PATH is not set.", file=sys.stderr)
        return 2

    event = json.loads(Path(event_path).read_text(encoding="utf-8"))
    pull_request = event.get("pull_request") or {}
    author = (pull_request.get("user") or {}).get("login", "")
    if author == "dependabot[bot]":
        print("Skipping Dependabot PR (bot-generated message).")
        return 0

    failures: list[str] = []

    title = (pull_request.get("title") or "").strip()
    number = pull_request.get("number")
    subject = title
    if number and not re.search(rf"\(#{number}\)$", subject):
        subject = f"{subject} (#{number})"
    if not title:
        failures.append("PR title is empty.")
    else:
        if not _CONVENTIONAL_SUBJECT.match(subject):
            failures.append(
                f"PR title does not become a Conventional Commit subject: '{subject}'"
            )
        if len(subject) > 72:
            failures.append(
                f"PR title becomes a subject longer than 72 characters: '{subject}'"
            )
        if title.endswith(".") or subject.endswith("."):
            failures.append(f"PR title must not end with a period: '{title}'")

    body = pull_request.get("body") or ""
    in_fence = False
    for line_number, line in enumerate(body.splitlines(), start=1):
        stripped = line.strip()
        if stripped.startswith("```"):
            in_fence = not in_fence
            continue
        if in_fence or not stripped:
            continue
        if stripped.startswith("|") or stripped.startswith(("http://", "https://")):
            continue
        if len(line) > 72:
            failures.append(f"body line {line_number} is longer than 72 characters: {line}")

    if failures:
        print("The PR title/body become the squash commit message and must follow "
              "the commit-message rules:", file=sys.stderr)
        for failure in failures:
            print(f"  - {failure}", file=sys.stderr)
        return 1

    print("PR title/body lint: OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
