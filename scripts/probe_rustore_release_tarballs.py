#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import io
import json
import re
import tarfile
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts" / "rustore-release-artifact-probe-hosted"
OUT.mkdir(parents=True, exist_ok=True)

PACKAGES = [
    {
        "label": "Install Referrer",
        "id": "ru.rustore.installreferrer",
        "version": "10.6.1",
        "tarball": "https://gitflic.ru/project/rustore/unity-rustore-install-referrer-sdk/release/2f17b04e-716e-4a9e-bba4-cfda7dc0b6aa/7cdfd7da-9326-4a9e-856d-a2d041b2b170/download",
        "checksum": "https://gitflic.ru/project/rustore/unity-rustore-install-referrer-sdk/release/2f17b04e-716e-4a9e-bba4-cfda7dc0b6aa/719b37a2-7cb7-437a-8ee2-d246588c6da8/download",
    },
    {
        "label": "Remote Config",
        "id": "ru.rustore.remoteconfig",
        "version": "10.5.1",
        "tarball": "https://gitflic.ru/project/rustore/unity-rustore-remote-config-sdk/release/e5557a31-215f-4ff5-92ec-3ded2d797346/ed94525e-816a-47fd-ba23-391eba907d6b/download",
        "checksum": "https://gitflic.ru/project/rustore/unity-rustore-remote-config-sdk/release/e5557a31-215f-4ff5-92ec-3ded2d797346/466b5740-38be-4a36-b23e-ea1629c85d44/download",
    },
]

GUID_RE = re.compile(r"(?im)^guid:\s*([0-9a-f]{32})\s*$")
SHA_RE = re.compile(r"(?i)\b[0-9a-f]{64}\b")


def fetch(url: str) -> bytes:
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": "rustore-game-release-artifact-probe/1.0",
            "Accept": "*/*",
        },
    )
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def safe_extract(tar: tarfile.TarFile, target: Path) -> None:
    target_resolved = target.resolve()
    for member in tar.getmembers():
        member_target = (target / member.name).resolve()
        if target_resolved not in member_target.parents and member_target != target_resolved:
            raise RuntimeError(f"Unsafe tar path: {member.name}")
    tar.extractall(target)


def meta_guids(root: Path) -> dict[str, list[str]]:
    result: dict[str, list[str]] = {}
    for path in root.rglob("*.meta"):
        text = path.read_text(encoding="utf-8", errors="replace")
        match = GUID_RE.search(text)
        if not match:
            continue
        result.setdefault(match.group(1).lower(), []).append(path.relative_to(root).as_posix())
    return result


results = []
for spec in PACKAGES:
    print(f"Downloading {spec['label']} {spec['version']}...")
    tar_bytes = fetch(spec["tarball"])
    checksum_text = fetch(spec["checksum"]).decode("utf-8", errors="replace")
    sha_match = SHA_RE.search(checksum_text)
    if not sha_match:
        raise SystemExit(f"Could not parse SHA-256 for {spec['label']}")

    expected = sha_match.group(0).lower()
    actual = hashlib.sha256(tar_bytes).hexdigest()
    if actual != expected:
        raise SystemExit(
            f"{spec['label']} SHA-256 mismatch: expected={expected} actual={actual}"
        )

    tgz = OUT / f"{spec['id']}-{spec['version']}.tgz"
    tgz.write_bytes(tar_bytes)
    extract = OUT / f"{spec['id']}-extract"
    extract.mkdir(parents=True, exist_ok=True)
    with tarfile.open(fileobj=io.BytesIO(tar_bytes), mode="r:gz") as tar:
        safe_extract(tar, extract)

    package_jsons = list(extract.rglob("package.json"))
    if not package_jsons:
        raise SystemExit(f"{spec['label']} package.json not found")
    package = json.loads(package_jsons[0].read_text(encoding="utf-8"))
    if package.get("name") != spec["id"] or package.get("version") != spec["version"]:
        raise SystemExit(
            f"{spec['label']} identity mismatch: {package.get('name')} {package.get('version')}"
        )

    results.append(
        {
            **spec,
            "sha256": actual,
            "meta": meta_guids(extract),
        }
    )
    print(f"{spec['label']}: checksum and package identity verified.")

left, right = results
collisions = []
for guid, left_paths in left["meta"].items():
    if guid not in right["meta"]:
        continue
    collisions.append(
        {
            "guid": guid,
            "install_referrer": left_paths,
            "remote_config": right["meta"][guid],
        }
    )

summary = {
    "packages": [
        {"id": item["id"], "version": item["version"], "sha256": item["sha256"]}
        for item in results
    ],
    "duplicate_guid_count": len(collisions),
    "duplicate_guids": collisions,
}
(OUT / "tarball-guid-summary.json").write_text(
    json.dumps(summary, ensure_ascii=False, indent=2),
    encoding="utf-8",
)

lines = [
    "RuStore official release tarball inspection",
    *(f"{x['id']} {x['version']} SHA256={x['sha256']}" for x in results),
    f"Cross-package duplicate GUID count: {len(collisions)}",
]
for collision in collisions:
    lines.append(f"GUID {collision['guid']}")
    lines.append("  InstallReferrer: " + "; ".join(collision["install_referrer"]))
    lines.append("  RemoteConfig:    " + "; ".join(collision["remote_config"]))
(OUT / "tarball-guid-summary.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
print("\n".join(lines))

if collisions:
    raise SystemExit("Official release tarballs contain cross-package duplicate GUIDs.")

print("Official release tarballs have no cross-package duplicate GUIDs.")
