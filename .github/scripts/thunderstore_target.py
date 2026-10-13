"""Reads where to publish on Thunderstore from PassiveSkillHud.csproj into the step's namespace, community and
categories outputs."""

import json
import os
import subprocess


def main():
    properties = json.loads(
        subprocess.run(
            [
                "dotnet",
                "msbuild",
                "PassiveSkillHud/PassiveSkillHud.csproj",
                "-getProperty:Authors",
                "-getProperty:ThunderstoreCommunity",
                "-getProperty:ThunderstoreCategories",
            ],
            stdout=subprocess.PIPE,
            text=True,
            check=True,
        ).stdout
    )["Properties"]
    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
        output.write(f"namespace={properties['Authors']}\n")
        output.write(f"community={properties['ThunderstoreCommunity']}\n")
        output.write(f"categories={properties['ThunderstoreCategories']}\n")


if __name__ == "__main__":
    main()
