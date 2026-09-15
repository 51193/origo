#!/usr/bin/env python3
"""Meta guard for the Origo agent-instruction system.

Checks the root AGENTS.md, which is injected every session, for:
- size budget (keeps the every-turn context short);
- mandatory section anchors and hard-gate commands;
- existence of every local Markdown link target;
- known forbidden patterns (for example the historical wrong docsync-pair example);
- the tracked docs/META authority sections and their anti-drift rules.

Run from scripts/lint-scripts.sh so CI fails when the guard rails drift.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
AGENTS = ROOT / "AGENTS.md"
MAX_LINES = 300
MAX_BYTES = 16 * 1024

REQUIRED_HEADINGS = tuple(f"### 1.{number} " for number in range(1, 12))
REQUIRED_SNIPPETS = (
    "bash scripts/test.sh",
    "bash scripts/ci.sh",
    "bash scripts/lint-commits.sh",
    "DocSyncTool -- generate",
    "BREAKING:",
    "_origo_local/",
    "mirror README file list",
    "mirrored source directory",
    "§Local Agent Work Buffer",
    "single-book mode",
    "root README",
    "numbered chapter",
    "docs/release-process.zh.md",
)
FORBIDDEN_PATTERNS = (
    re.compile(r"docsync-pair:\s*docs/"),
    re.compile(r"Before committing.{0,80}scripts/ci\.sh"),
    re.compile(r"relevant `inbox` / `in-progress` / `blocked` item"),
)
META_FILES = (
    ("en", ROOT / "docs/META.en.md"),
    ("zh", ROOT / "docs/META.zh.md"),
)
META_REQUIRED_SNIPPETS = {
    "en": (
        "Environment Bootstrap",
        "Local Agent Work Buffer",
        "single-book work buffer",
        "Navigation-only directories",
        "<name>.zh.md",
        "Test capability/method",
        "related tests",
        "relevant git history",
        "unverified",
    ),
    "zh": (
        "环境引导",
        "本地 Agent 工作缓冲",
        "单册工作缓冲",
        "纯导航目录",
        "<name>.zh.md",
        "测试能力/方法",
        "相关测试",
        "相关 git 历史",
        "未验证",
    ),
}
META_FORBIDDEN_PATTERNS = (
    re.compile(r"AGENTS\.md\s*§"),
    re.compile(r"Every directory contains"),
    re.compile(r"Content directories contain the language pair below"),
    re.compile(r"每个目录下[：:]"),
    re.compile(r"内容目录包含下面的语言对"),
)
LINK_PATTERN = re.compile(r"(?<!!)\[[^\]]*\]\(([^)]+)\)")
EXTERNAL_PREFIXES = ("http://", "https://", "mailto:")


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)


def main() -> int:
    if not AGENTS.is_file():
        fail("AGENTS.md is missing; it is the repository marker for DocSyncTool and the agent entry point.")
        return 1

    text = AGENTS.read_text(encoding="utf-8")
    lines = text.splitlines()
    byte_count = len(text.encode("utf-8"))
    errors: list[str] = []

    if len(lines) > MAX_LINES:
        errors.append(f"AGENTS.md has {len(lines)} lines (budget: {MAX_LINES}); move detail into docs/ instead of growing the injected context.")
    if byte_count > MAX_BYTES:
        errors.append(f"AGENTS.md has {byte_count} bytes (budget: {MAX_BYTES}); move detail into docs/ instead of growing the injected context.")

    for heading in REQUIRED_HEADINGS:
        if not any(line.startswith(heading) for line in lines):
            errors.append(f"AGENTS.md is missing mandatory heading '{heading.strip()}'.")

    for snippet in REQUIRED_SNIPPETS:
        if snippet not in text:
            errors.append(f"AGENTS.md is missing mandatory hard-gate text: '{snippet}'.")

    for pattern in FORBIDDEN_PATTERNS:
        if pattern.search(text):
            errors.append(f"AGENTS.md contains forbidden pattern: /{pattern.pattern}/")

    for language, meta_path in META_FILES:
        if not meta_path.is_file():
            errors.append(f"{meta_path.relative_to(ROOT)} is missing; META is the tracked authority for environment, DocSync, and buffer rules.")
            continue

        meta_text = meta_path.read_text(encoding="utf-8")
        for snippet in META_REQUIRED_SNIPPETS[language]:
            if snippet not in meta_text:
                errors.append(f"{meta_path.relative_to(ROOT)} is missing mandatory authority text: '{snippet}'.")
        for pattern in META_FORBIDDEN_PATTERNS:
            if pattern.search(meta_text):
                errors.append(f"{meta_path.relative_to(ROOT)} contains forbidden drift pattern: /{pattern.pattern}/")

    for match in LINK_PATTERN.finditer(text):
        raw_target = match.group(1).strip()
        if raw_target.startswith(EXTERNAL_PREFIXES):
            continue
        target = raw_target.split("#", 1)[0]
        if not target:
            continue
        resolved = (AGENTS.parent / target).resolve()
        if not resolved.exists():
            errors.append(f"AGENTS.md link target does not exist: '{raw_target}' -> {resolved.relative_to(ROOT) if resolved.is_relative_to(ROOT) else resolved}")

    if errors:
        print("Agent-doc guard FAILED:", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        return 1

    print(f"Agent-doc guard: OK ({len(lines)} lines, {byte_count} bytes).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
