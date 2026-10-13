"""Checks that the pushed tag and CHANGELOG.md match the version in PassiveSkillHud.csproj."""

import os
import subprocess
import sys
from pathlib import Path


def fail(message):
    print(f"::error::{message}")
    sys.exit(1)


def main():
    version = subprocess.run(
        [
            "dotnet",
            "msbuild",
            "PassiveSkillHud/PassiveSkillHud.csproj",
            "-getProperty:Version",
        ],
        stdout=subprocess.PIPE,
        text=True,
        check=True,
    ).stdout.strip()
    tag = os.environ["GITHUB_REF_NAME"]
    if tag != f"v{version}":
        fail(
            f"Tag {tag} doesn't match the version in PassiveSkillHud.csproj ({version})."
        )

    # Each version in CHANGELOG.md has its own heading, e.g. "## 1.0.0".
    lines = Path("CHANGELOG.md").read_text(encoding="utf-8").splitlines()
    if f"## {version}" not in lines:
        fail(f"CHANGELOG.md has no entry for {version}.")


if __name__ == "__main__":
    main()
