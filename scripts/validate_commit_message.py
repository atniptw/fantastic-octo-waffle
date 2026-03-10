#!/usr/bin/env python3
from __future__ import annotations

import argparse
import re
import sys

ALLOWED_TYPES = (
    "feat",
    "fix",
    "docs",
    "style",
    "refactor",
    "perf",
    "test",
    "build",
    "ci",
    "chore",
    "revert",
)

HEADER_PATTERN = re.compile(
    r"^(?P<type>feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)"
    r"(?:\((?P<scope>[a-z0-9][a-z0-9._/-]*)\))?"
    r"(?P<breaking>!)?: "
    r"(?P<description>.+)$"
)


def validate_header(header: str) -> tuple[bool, str | None]:
    match = HEADER_PATTERN.match(header)
    if not match:
        return False, (
            "invalid header format; expected 'type(scope)?: description' "
            f"with type in {{{', '.join(ALLOWED_TYPES)}}}"
        )

    description = match.group("description").strip()
    if not description:
        return False, "description must not be empty"
    if description.endswith("."):
        return False, "description should not end with a period"

    return True, None


def has_breaking_footer(lines: list[str]) -> bool:
    return any(line.startswith("BREAKING CHANGE:") for line in lines)


def validate_message(raw: str) -> tuple[bool, list[str]]:
    lines = [line.rstrip() for line in raw.splitlines()]
    if not lines:
        return False, ["message is empty"]

    header = lines[0]
    valid_header, header_error = validate_header(header)
    errors: list[str] = []
    if not valid_header and header_error:
        errors.append(header_error)

    breaking_in_header = "!" in header.split(":", 1)[0]
    breaking_in_footer = has_breaking_footer(lines[1:])
    if breaking_in_footer and not valid_header:
        errors.append("BREAKING CHANGE footer requires a valid conventional header")

    if not errors:
        if breaking_in_header and not breaking_in_footer:
            # Allowed by spec; no footer is required if header uses !.
            return True, []
        return True, []

    return False, errors


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate Conventional Commits v1.0.0 header")
    parser.add_argument(
        "message",
        nargs="?",
        help="Commit message string. If omitted, read from --message-file.",
    )
    parser.add_argument(
        "--message-file",
        help="Path to commit message file used by git commit-msg hooks.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()

    if args.message_file:
        try:
            raw = open(args.message_file, encoding="utf-8").read()
        except OSError as exc:
            print(f"error: unable to read message file: {exc}", file=sys.stderr)
            return 2
    elif args.message is not None:
        raw = args.message
    else:
        print("error: provide message text or --message-file", file=sys.stderr)
        return 2

    ok, errors = validate_message(raw)
    if ok:
        print("commit message valid")
        return 0

    print("commit message invalid:", file=sys.stderr)
    for error in errors:
        print(f"- {error}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
