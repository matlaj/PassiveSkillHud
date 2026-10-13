"""Publishes the package zip in dist/ to Thunderstore.

Takes the service account token from THUNDERSTORE_TOKEN, and where to publish from NAMESPACE, COMMUNITY and
CATEGORIES (separated by semicolons).
"""

import contextlib
import json
import os
import urllib.error
import urllib.request
from pathlib import Path

API = "https://thunderstore.io/api/experimental"

# Seconds to wait on a stalled connection, so that a request that never answers fails the job instead of hanging it.
TIMEOUT = 60

# Thunderstore's Cloudflare front refuses urllib's default "Python-urllib/3.x" with error 1010.
USER_AGENT = "PassiveSkillHud-release (+https://github.com/matlaj/PassiveSkillHud)"


def send(request, name):
    """Sends a request and returns its response body and headers."""
    request.add_header("User-Agent", USER_AGENT)
    try:
        with urllib.request.urlopen(request, timeout=TIMEOUT) as response:
            return response.read(), response.headers
    except urllib.error.HTTPError as error:
        # Thunderstore and the storage behind its upload URLs explain what they rejected in the response body, e.g. a
        # version that's already published or an upload URL that has expired.
        raise RuntimeError(
            f"{name} failed with {error.code}: {error.read().decode(errors='replace')}"
        ) from None


def call(path, body=None):
    """POSTs to a Thunderstore API endpoint and returns its JSON response, if any."""
    request = urllib.request.Request(
        f"{API}/{path}",
        data=None if body is None else json.dumps(body).encode(),
        headers={
            "Authorization": f"Bearer {os.environ['THUNDERSTORE_TOKEN']}",
            "Content-Type": "application/json",
        },
        method="POST",
    )
    content, _ = send(request, path)
    return json.loads(content) if content else None


def upload(zip_path):
    """Uploads the zip and returns its Thunderstore upload UUID."""
    data = zip_path.read_bytes()

    # The zip goes up as a multipart upload: Thunderstore hands out a pre-signed URL for each part, and the ETags those
    # return finish it.
    initiated = call(
        "usermedia/initiate-upload/",
        {"filename": zip_path.name, "file_size_bytes": len(data)},
    )
    uuid = initiated["user_media"]["uuid"]
    try:
        parts = []
        for part in initiated["upload_urls"]:
            chunk = data[part["offset"] : part["offset"] + part["length"]]
            request = urllib.request.Request(
                part["url"],
                data=chunk,
                headers={"Content-Type": "application/octet-stream"},
                method="PUT",
            )
            _, headers = send(request, f"Uploading part {part['part_number']}")
            parts.append({"ETag": headers["ETag"], "PartNumber": part["part_number"]})
        call(f"usermedia/{uuid}/finish-upload/", {"parts": parts})
    except Exception:
        with contextlib.suppress(Exception):
            call(f"usermedia/{uuid}/abort-upload/")
        raise
    return uuid


def main():
    namespace, community = os.environ["NAMESPACE"], os.environ["COMMUNITY"]
    zip_path = next(Path("dist").glob("*.zip"))
    result = call(
        "submission/submit/",
        {
            "upload_uuid": upload(zip_path),
            "author_name": namespace,
            "communities": [community],
            "community_categories": {community: os.environ["CATEGORIES"].split(";")},
            "has_nsfw_content": False,
        },
    )
    version = result["package_version"]
    print(
        f"Published {version['full_name']}: "
        f"https://thunderstore.io/c/{community}/p/{version['namespace']}/{version['name']}/"
    )


if __name__ == "__main__":
    main()
