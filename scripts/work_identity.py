#!/usr/bin/env python3
"""Branch and worktree identity rules for Origo development workflows."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import subprocess
import sys
from pathlib import Path
from typing import Sequence

WORK_TYPES = frozenset({
    "feat",
    "fix",
    "refactor",
    "perf",
    "docs",
    "test",
    "chore",
    "build",
    "ci",
    "style",
    "revert",
})
CREATOR_PATTERN = re.compile(r"^[a-z0-9](?:[a-z0-9-]{0,37}[a-z0-9])?$")
PURPOSE_PATTERN = re.compile(r"^(?:[0-9]+-)?[a-z0-9]+(?:-[a-z0-9]+)*$")
BRANCH_PATTERN = re.compile(r"^(?P<type>[^/]+)/(?P<creator>[^/]+)/(?P<date>[^/]+)/(?P<purpose>[^/]+)$")


class WorkIdentityError(ValueError):
    """Raised when a branch/worktree identity cannot satisfy its contract."""


def normalize_creator(value: str) -> str:
    creator = value.strip()
    if creator != creator.lower():
        raise WorkIdentityError("creator must be lowercase")
    if not CREATOR_PATTERN.fullmatch(creator):
        raise WorkIdentityError(
            "creator must be a lowercase GitHub login using 1-39 letters, digits, or hyphens"
        )
    return creator


def normalize_purpose(value: str, issue_number: str | None = None) -> str:
    purpose = value.strip()
    if purpose != purpose.lower():
        raise WorkIdentityError("purpose must be lowercase kebab-case")
    if issue_number is not None:
        if not issue_number.isdigit() or int(issue_number) <= 0:
            raise WorkIdentityError("issue number must be a positive integer")
        purpose = f"{issue_number}-{purpose}"
    if not PURPOSE_PATTERN.fullmatch(purpose) or not re.search(r"[a-z]", purpose):
        raise WorkIdentityError(
            "purpose must be lowercase kebab-case and contain at least one letter"
        )
    return purpose


def validate_month_day(value: str) -> str:
    if not re.fullmatch(r"(?:0[1-9]|1[0-2])(?:0[1-9]|[12][0-9]|3[01])", value):
        raise WorkIdentityError("date must use valid MMDD syntax")
    try:
        dt.date(2000, int(value[:2]), int(value[2:]))
    except ValueError as exc:
        raise WorkIdentityError("date must be a real calendar date") from exc
    return value


def build_names(
    *,
    work_type: str,
    creator: str,
    month_day: str,
    purpose: str,
    issue_number: str | None = None,
    repository_name: str = "origo",
) -> tuple[str, str]:
    if work_type not in WORK_TYPES:
        raise WorkIdentityError(f"type must be one of: {', '.join(sorted(WORK_TYPES))}")
    normalized_creator = normalize_creator(creator)
    normalized_date = validate_month_day(month_day)
    normalized_purpose = normalize_purpose(purpose, issue_number)
    if not re.fullmatch(r"[a-z0-9][a-z0-9.-]*", repository_name):
        raise WorkIdentityError("repository name must contain only lowercase letters, digits, dots, or hyphens")
    branch = f"{work_type}/{normalized_creator}/{normalized_date}/{normalized_purpose}"
    worktree = f"{repository_name}--{work_type}--{normalized_creator}--{normalized_date}--{normalized_purpose}"
    return branch, worktree


def _strip_ref_name(value: str) -> str:
    if value.startswith("refs/heads/"):
        return value.removeprefix("refs/heads/")
    return value


def validate_branch_name(value: str) -> tuple[bool, str]:
    branch = _strip_ref_name(value.strip())
    if branch in {"main", "master"} or branch.startswith("dependabot/") or branch.startswith("refs/tags/"):
        return True, "exempt context"
    match = BRANCH_PATTERN.fullmatch(branch)
    if match is None:
        return False, "branch must match <type>/<creator>/<MMDD>/<purpose> format"
    try:
        build_names(
            work_type=match.group("type"),
            creator=match.group("creator"),
            month_day=match.group("date"),
            purpose=match.group("purpose"),
            repository_name="origo",
        )
    except WorkIdentityError as exc:
        return False, str(exc)
    return True, "valid branch name"


def _run(command: Sequence[str], *, cwd: Path | None = None) -> str:
    result = subprocess.run(command, cwd=cwd, check=False, capture_output=True, text=True)
    if result.returncode != 0:
        detail = result.stderr.strip() or result.stdout.strip() or "command failed"
        raise WorkIdentityError(f"{' '.join(command)}: {detail}")
    return result.stdout.strip()


def resolve_creator(explicit: str | None, repository: Path) -> str:
    if explicit:
        return normalize_creator(explicit)
    configured = subprocess.run(
        ["git", "-C", str(repository), "config", "--local", "--get", "origo.githubUser"],
        check=False,
        capture_output=True,
        text=True,
    ).stdout.strip()
    if configured:
        return normalize_creator(configured)
    try:
        return normalize_creator(_run(["gh", "api", "user", "--jq", ".login"]))
    except (FileNotFoundError, WorkIdentityError):
        raise WorkIdentityError(
            "cannot resolve creator; pass --creator, set git config --local origo.githubUser, or authenticate gh"
        ) from None


def _repository_root(repository: Path) -> Path:
    return Path(_run(["git", "-C", str(repository), "rev-parse", "--show-toplevel"])).resolve()


def _main_checkout_root(repository: Path) -> Path:
    """Return the checkout containing main/master when invoked from a worktree."""
    root = _repository_root(repository)
    listing = _run(["git", "-C", str(root), "worktree", "list", "--porcelain"])
    for block in listing.strip().split("\n\n") if listing.strip() else []:
        lines = block.splitlines()
        path_line = next((line for line in lines if line.startswith("worktree ")), None)
        is_main = any(line in {"branch refs/heads/main", "branch refs/heads/master"} for line in lines)
        if path_line and is_main:
            return Path(path_line.removeprefix("worktree ")).resolve()
    return root


def _current_branch(repository: Path) -> str | None:
    result = subprocess.run(
        ["git", "-C", str(repository), "symbolic-ref", "--quiet", "--short", "HEAD"],
        check=False,
        capture_output=True,
        text=True,
    )
    branch = result.stdout.strip()
    if branch:
        return branch
    if os.environ.get("GITHUB_REF_TYPE") == "tag":
        return f"refs/tags/{os.environ.get('GITHUB_REF_NAME', 'detached')}"
    exact_tag = subprocess.run(
        ["git", "-C", str(repository), "describe", "--exact-match", "--tags", "HEAD"],
        check=False,
        capture_output=True,
        text=True,
    ).stdout.strip()
    return f"refs/tags/{exact_tag}" if exact_tag else None


def command_validate(branch: str | None, repository: Path) -> int:
    actual = branch or os.environ.get("ORIGO_BRANCH_NAME") or _current_branch(repository)
    if not actual:
        print("ERROR: cannot determine branch name from this detached checkout; pass --branch or set ORIGO_BRANCH_NAME.", file=sys.stderr)
        return 1
    valid, reason = validate_branch_name(actual)
    if valid:
        print(f"Branch identity: OK ({actual}; {reason})")
        return 0
    print(f"ERROR: invalid branch identity '{actual}': {reason}", file=sys.stderr)
    return 1


def _build_from_args(args: argparse.Namespace, repository: Path) -> tuple[str, str]:
    creator = resolve_creator(args.creator, repository)
    month_day = args.date or dt.datetime.now().astimezone().strftime("%m%d")
    repository_name = _main_checkout_root(repository).name.lower()
    return build_names(
        work_type=args.type,
        creator=creator,
        month_day=month_day,
        purpose=args.purpose,
        issue_number=args.issue,
        repository_name=repository_name,
    )


def command_name(args: argparse.Namespace, repository: Path) -> int:
    branch, worktree = _build_from_args(args, repository)
    if args.format == "branch":
        print(branch)
    elif args.format == "worktree":
        print(worktree)
    else:
        print(json.dumps({"branch": branch, "worktree": worktree}, sort_keys=True))
    return 0


def command_new(args: argparse.Namespace, repository: Path) -> int:
    branch, worktree_name = _build_from_args(args, repository)
    root = _repository_root(repository)
    checkout_root = _main_checkout_root(repository)
    worktree_root = Path(args.worktree_root).expanduser().resolve() if args.worktree_root else checkout_root.parent
    worktree_root.mkdir(parents=True, exist_ok=True)
    worktree_path = worktree_root / worktree_name
    if worktree_path.exists():
        raise WorkIdentityError(f"worktree path already exists: {worktree_path}")
    base = args.base or "HEAD"
    _run(["git", "-C", str(root), "worktree", "add", "-b", branch, str(worktree_path), base])
    print(f"Branch: {branch}")
    print(f"Worktree: {worktree_path}")
    return 0


def _add_identity_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--repository", type=Path, default=Path.cwd())
    parser.add_argument("--type", required=True, choices=sorted(WORK_TYPES))
    parser.add_argument("--creator")
    parser.add_argument("--purpose", required=True)
    parser.add_argument("--issue")
    parser.add_argument("--date", help="creation date in MMDD form; defaults to the local date")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    validate = subparsers.add_parser("validate", help="validate the current or supplied branch")
    validate.add_argument("--repository", type=Path, default=Path.cwd())
    validate.add_argument("--branch")

    name = subparsers.add_parser("name", help="print derived branch/worktree names")
    _add_identity_arguments(name)
    name.add_argument("--format", choices=("branch", "worktree", "json"), default="json")

    new = subparsers.add_parser("new", help="create a branch and sibling worktree")
    _add_identity_arguments(new)
    new.add_argument("--base", help="base ref for the new worktree; defaults to HEAD")
    new.add_argument("--worktree-root", type=Path)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    repository = args.repository.expanduser().resolve()
    try:
        if args.command == "validate":
            return command_validate(args.branch, repository)
        if args.command == "name":
            return command_name(args, repository)
        if args.command == "new":
            return command_new(args, repository)
    except (OSError, WorkIdentityError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1
    raise AssertionError(f"unhandled command: {args.command}")


if __name__ == "__main__":
    raise SystemExit(main())
