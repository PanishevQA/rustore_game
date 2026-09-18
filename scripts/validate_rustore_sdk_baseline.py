import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VERSIONS = ROOT / "UnityProject/Assets/Game/Platform/RuStore/RuStoreSdkVersions.cs"
PACKAGES = ROOT / "UnityProject/Packages/manifest.json"
MATRIX = ROOT / "docs/RUSTORE_SDK_MATRIX.md"
CHECKLIST = ROOT / "docs/RUSTORE_RELEASE_CHECKLIST.md"
README = ROOT / "README.md"
STATUS = ROOT / "docs/MVP_STATUS.md"

verified_date = "2026-09-17"
expected = {
    "Pay": "11.1.0",
    "InstallReferrer": "10.6.1",
    "Update": "10.5.1",
    "Review": "10.5.1",
    "GameCenter": "10.5.2",
    "RemoteConfig": "10.5.1",
}
installed_packages = {
    # Local Unity Editor baseline intentionally contains no RuStore packages.
    # Exact production targets remain documented and are enforced before release.
}

versions_text = VERSIONS.read_text(encoding="utf-8")
matrix_text = MATRIX.read_text(encoding="utf-8")
checklist_text = CHECKLIST.read_text(encoding="utf-8")
readme_text = README.read_text(encoding="utf-8")
status_text = STATUS.read_text(encoding="utf-8")
manifest_text = PACKAGES.read_text(encoding="utf-8")
manifest = json.loads(manifest_text)
dependencies = manifest.get("dependencies") or {}


def constant(name: str) -> str | None:
    match = re.search(rf'public const string {re.escape(name)}\s*=\s*"([^"]+)";', versions_text)
    return match.group(1) if match else None


errors: list[str] = []

if constant("LastVerifiedUtc") != verified_date:
    errors.append(f"RuStore SDK LastVerifiedUtc must be {verified_date}.")

for name, value in expected.items():
    actual = constant(name)
    if actual != value:
        errors.append(f"RuStore SDK {name} must stay pinned to {value}; found {actual!r}.")

for package, value in installed_packages.items():
    actual = dependencies.get(package)
    if actual != value:
        errors.append(f"Installed package {package} must match verified baseline {value}; found {actual!r}.")

if not matrix_text.startswith(f"# RuStore SDK matrix — {verified_date}\n"):
    errors.append("RUSTORE_SDK_MATRIX.md verification date does not match RuStoreSdkVersions.LastVerifiedUtc.")

for name, value in expected.items():
    if value not in matrix_text:
        errors.append(f"RUSTORE_SDK_MATRIX.md does not document the verified {name} version {value}.")

checklist_requirements = {
    "Pay": f"Pay {expected['Pay']}",
    "InstallReferrer": f"Install Referrer Unity {expected['InstallReferrer']}",
    "RemoteConfig": f"текущий проверенный Unity target — **{expected['RemoteConfig']}**",
}
for name, marker in checklist_requirements.items():
    if marker not in checklist_text:
        errors.append(
            f"RUSTORE_RELEASE_CHECKLIST.md is not synchronized with the verified {name} baseline {expected[name]}."
        )

release_docs = {
    "README.md": (
        readme_text,
        (
            f"Последняя сверка RuStore targets — {verified_date}:",
            f"Pay Unity `{expected['Pay']}`",
            f"Install Referrer Unity `{expected['InstallReferrer']}`",
            f"Remote Config Unity `{expected['RemoteConfig']}`",
        ),
    ),
    "docs/MVP_STATUS.md": (
        status_text,
        (
            f"Последняя сверка RuStore targets на {verified_date}:",
            f"Pay Unity: `{expected['Pay']}`",
            f"Install Referrer Unity: `{expected['InstallReferrer']}`",
            f"Remote Config Unity: **`{expected['RemoteConfig']}`**",
        ),
    ),
}
for path, (text, markers) in release_docs.items():
    for marker in markers:
        if marker not in text:
            errors.append(f"{path} is not synchronized with the verified RuStore baseline: missing {marker!r}.")

npm_registry = constant("NpmRegistry")
registries = manifest.get("scopedRegistries") or []
if not any(
    entry.get("url") == npm_registry and "ru.rustore" in (entry.get("scopes") or [])
    for entry in registries
):
    errors.append("Packages/manifest.json RuStore registry must match RuStoreSdkVersions.NpmRegistry.")

if "artifactory-external.vkpartner.ru" in versions_text or "artifactory-external.vkpartner.ru" in manifest_text:
    errors.append("Deprecated RuStore repository address detected in active package configuration.")

if errors:
    raise SystemExit("RuStore SDK baseline validation failed:\n- " + "\n- ".join(errors))

print("RuStore SDK baseline validation passed.")
