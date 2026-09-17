#!/usr/bin/env python3
import hashlib
import importlib.util
import json
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "scripts/verify_release_artifact.py"
spec = importlib.util.spec_from_file_location("verify_release_artifact", MODULE_PATH)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

with tempfile.TemporaryDirectory() as temporary:
    directory = Path(temporary)
    artifact = directory / "nesbeisya-1.2.3-42.aab"
    artifact.write_bytes(b"fake-aab-payload-for-integrity-test")
    payload = artifact.read_bytes()

    metadata = {
        "packageName": "ru.panishedqa.nesbeisya",
        "version": "1.2.3",
        "versionCode": 42,
        "unityVersion": "6000.3.24f1",
        "buildGuid": "0123456789abcdef0123456789abcdef",
        "builtAtUtc": "2026-09-17T14:00:00.0000000Z",
        "gitCommit": "abcdef123456",
        "artifactFile": artifact.name,
        "artifactSizeBytes": len(payload),
        "artifactSha256": hashlib.sha256(payload).hexdigest(),
        "unityReportedSizeBytes": len(payload),
    }
    metadata_path = directory / "nesbeisya-1.2.3-42.release.json"
    metadata_path.write_text(json.dumps(metadata), encoding="utf-8")

    verified_path, digest = module.verify(
        metadata_path,
        expected_package="ru.panishedqa.nesbeisya",
        expected_version="1.2.3",
        expected_version_code=42,
        expected_git_sha="abcdef123456",
    )
    assert verified_path == artifact
    assert digest == metadata["artifactSha256"]

    artifact.write_bytes(b"Fake-aab-payload-for-integrity-test")
    try:
        module.verify(metadata_path)
    except ValueError as error:
        assert "SHA-256 mismatch" in str(error), error
    else:
        raise AssertionError("Verifier accepted a modified AAB with stale metadata hash")

    artifact.write_bytes(payload)
    try:
        module.verify(metadata_path, expected_version_code=43)
    except ValueError as error:
        assert "versionCode mismatch" in str(error), error
    else:
        raise AssertionError("Verifier accepted an unexpected versionCode")

print("release artifact verifier tests passed")
