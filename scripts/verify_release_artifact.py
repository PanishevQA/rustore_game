#!/usr/bin/env python3
import argparse
import hashlib
import json
from pathlib import Path

DEV_PACKAGE = "ru.panishedqa.nesbeisya.dev"
DEV_VERSION = "0.1.0"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def verify(metadata_path: Path, expected_package=None, expected_version=None, expected_version_code=None, expected_git_sha=None):
    metadata_path = metadata_path.resolve()
    if not metadata_path.is_file():
        raise ValueError(f"Release metadata not found: {metadata_path}")

    try:
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    except Exception as error:
        raise ValueError(f"Release metadata is not valid JSON: {error}") from error

    required = (
        "packageName",
        "version",
        "versionCode",
        "unityVersion",
        "buildGuid",
        "builtAtUtc",
        "artifactFile",
        "artifactSizeBytes",
        "artifactSha256",
    )
    missing = [key for key in required if key not in metadata]
    if missing:
        raise ValueError("Release metadata missing fields: " + ", ".join(missing))

    artifact_file = metadata["artifactFile"]
    if not isinstance(artifact_file, str) or not artifact_file or Path(artifact_file).name != artifact_file:
        raise ValueError("artifactFile must be a plain file name without directories")

    artifact_path = metadata_path.parent / artifact_file
    if artifact_path.suffix.lower() != ".aab":
        raise ValueError(f"Release artifact must be an AAB: {artifact_path.name}")
    if not artifact_path.is_file():
        raise ValueError(f"Release AAB not found next to metadata: {artifact_path}")

    actual_size = artifact_path.stat().st_size
    expected_size = metadata["artifactSizeBytes"]
    if not isinstance(expected_size, int) or expected_size <= 0:
        raise ValueError("artifactSizeBytes must be a positive integer")
    if actual_size != expected_size:
        raise ValueError(f"AAB size mismatch: metadata={expected_size}, actual={actual_size}")

    expected_hash = str(metadata["artifactSha256"]).lower()
    if len(expected_hash) != 64 or any(ch not in "0123456789abcdef" for ch in expected_hash):
        raise ValueError("artifactSha256 must be a 64-character lowercase/uppercase hex SHA-256")
    actual_hash = sha256_file(artifact_path)
    if actual_hash != expected_hash:
        raise ValueError(f"AAB SHA-256 mismatch: metadata={expected_hash}, actual={actual_hash}")

    package_name = str(metadata["packageName"]).strip()
    version = str(metadata["version"]).strip()
    version_code = metadata["versionCode"]
    if not package_name or package_name == DEV_PACKAGE or "placeholder" in package_name.lower() or "defaultcompany" in package_name.lower():
        raise ValueError(f"Release metadata contains a development/placeholder package: {package_name}")
    if not version or version == DEV_VERSION:
        raise ValueError(f"Release metadata contains the development version: {version}")
    if not isinstance(version_code, int) or version_code <= 0:
        raise ValueError(f"Release metadata contains invalid versionCode: {version_code}")
    if not str(metadata["unityVersion"]).strip():
        raise ValueError("unityVersion is empty")
    if not str(metadata["buildGuid"]).strip():
        raise ValueError("buildGuid is empty")

    if expected_package is not None and package_name != expected_package:
        raise ValueError(f"Package mismatch: expected={expected_package}, metadata={package_name}")
    if expected_version is not None and version != expected_version:
        raise ValueError(f"Version mismatch: expected={expected_version}, metadata={version}")
    if expected_version_code is not None and version_code != expected_version_code:
        raise ValueError(f"versionCode mismatch: expected={expected_version_code}, metadata={version_code}")
    if expected_git_sha is not None:
        metadata_git_sha = str(metadata.get("gitCommit", "")).strip()
        if not metadata_git_sha:
            raise ValueError("Expected a Git SHA, but release metadata gitCommit is empty")
        if metadata_git_sha != expected_git_sha:
            raise ValueError(f"Git SHA mismatch: expected={expected_git_sha}, metadata={metadata_git_sha}")

    return artifact_path, actual_hash


def main():
    parser = argparse.ArgumentParser(description="Verify НЕ СБЕЙСЯ! production AAB against adjacent release metadata.")
    parser.add_argument("metadata", type=Path, help="Path to <artifact>.release.json")
    parser.add_argument("--package", dest="expected_package")
    parser.add_argument("--version", dest="expected_version")
    parser.add_argument("--version-code", dest="expected_version_code", type=int)
    parser.add_argument("--git-sha", dest="expected_git_sha")
    args = parser.parse_args()

    try:
        artifact_path, digest = verify(
            args.metadata,
            expected_package=args.expected_package,
            expected_version=args.expected_version,
            expected_version_code=args.expected_version_code,
            expected_git_sha=args.expected_git_sha,
        )
    except ValueError as error:
        raise SystemExit(f"Release artifact verification failed: {error}")

    print(f"Release artifact verified: {artifact_path}")
    print(f"SHA-256: {digest}")


if __name__ == "__main__":
    main()
