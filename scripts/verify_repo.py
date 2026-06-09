#!/usr/bin/env python3
"""Repository verification script for AppWatchdog (.NET 8 / C#).

Checks:
1) All .csproj files exist and are valid XML
2) Solution file references correct projects
3) JSON config examples are valid
4) No appsettings.json committed (should be gitignored)
5) Required files exist
"""

from __future__ import annotations

import json
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import List, Tuple

ROOT = Path(__file__).resolve().parent.parent


class VerifyError(RuntimeError):
    pass


def _print_ok(msg: str) -> None:
    print(f"[PASS] {msg}")


def _print_ng(msg: str) -> None:
    print(f"[FAIL] {msg}")


def check_required_files() -> None:
    """Check that all required project files exist."""
    required = [
        "AppWatchdog.sln",
        "src/AppWatchdog.Core/AppWatchdog.Core.csproj",
        "src/AppWatchdog.App/AppWatchdog.App.csproj",
        "src/AppWatchdog.Cli/AppWatchdog.Cli.csproj",
        "config/appsettings.example.json",
        "README.md",
        "LICENSE",
        ".gitignore",
        ".github/workflows/ci.yml",
        ".github/workflows/release.yml",
        "docs/WINDOWS_USER_MANUAL_JA.md",
        "docs/AppWatchdog_Windows_User_Manual_JA.docx",
    ]
    missing = [f for f in required if not (ROOT / f).exists()]
    if missing:
        raise VerifyError(f"Missing required files: {missing}")
    _print_ok("required files exist")


def check_csproj_valid() -> None:
    """Check that all .csproj files are valid XML."""
    for csproj in ROOT.rglob("*.csproj"):
        try:
            ET.parse(str(csproj))
        except ET.ParseError as e:
            raise VerifyError(f"Invalid XML in {csproj}: {e}")
    _print_ok("all .csproj files are valid XML")


def check_sln_references() -> None:
    """Check that solution file references all expected projects."""
    sln_path = ROOT / "AppWatchdog.sln"
    sln_text = sln_path.read_text(encoding="utf-8")
    expected_projects = [
        "AppWatchdog.Core",
        "AppWatchdog.App",
        "AppWatchdog.Cli",
    ]
    missing = [p for p in expected_projects if p not in sln_text]
    if missing:
        raise VerifyError(f"Solution missing project references: {missing}")
    _print_ok("solution references all projects")


def check_json_configs() -> None:
    """Check that JSON config examples are valid."""
    example = ROOT / "config" / "appsettings.example.json"
    if example.exists():
        try:
            data = json.loads(example.read_text(encoding="utf-8"))
            if "targets" not in data:
                raise VerifyError("appsettings.example.json missing 'targets' key")
        except json.JSONDecodeError as e:
            raise VerifyError(f"Invalid JSON in appsettings.example.json: {e}")
    _print_ok("JSON config examples valid")


def check_no_secrets_committed() -> None:
    """Check that user config (appsettings.json) is not in the repo."""
    config_path = ROOT / "config" / "appsettings.json"
    if config_path.exists():
        # Check if it's gitignored
        gitignore = ROOT / ".gitignore"
        if gitignore.exists():
            text = gitignore.read_text(encoding="utf-8")
            if "appsettings.json" in text:
                _print_ok("appsettings.json is gitignored")
                return
        raise VerifyError("config/appsettings.json exists and may not be gitignored")
    _print_ok("no user config committed")


def check_cs_files_compile() -> None:
    """Basic check that .cs files have valid structure (open/close braces match)."""
    errors: List[Tuple[str, str]] = []
    for cs_file in ROOT.rglob("*.cs"):
        if "__pycache__" in str(cs_file) or "bin" in cs_file.parts or "obj" in cs_file.parts:
            continue
        try:
            text = cs_file.read_text(encoding="utf-8")
            open_count = text.count("{")
            close_count = text.count("}")
            if open_count != close_count:
                errors.append((str(cs_file.relative_to(ROOT)), f"brace mismatch: {{ ={open_count} }} ={close_count}"))
        except Exception as e:
            errors.append((str(cs_file.relative_to(ROOT)), str(e)))

    if errors:
        details = "\n".join([f"- {f}: {m}" for f, m in errors])
        raise VerifyError(f"C# file issues:\n{details}")
    _print_ok("C# files brace-balanced")


def main() -> int:
    checks = [
        check_required_files,
        check_csproj_valid,
        check_sln_references,
        check_json_configs,
        check_no_secrets_committed,
        check_cs_files_compile,
    ]

    try:
        for fn in checks:
            fn()
        print("VERIFY_OK")
        return 0
    except Exception as exc:
        _print_ng(str(exc))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
