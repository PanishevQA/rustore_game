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
NATIVE_REFERRER_DEPENDENCY = ROOT / "UnityProject/Assets/Game/Platform/RuStore/Editor/RuStoreNativeInstallReferrerDependencies.xml"
REFERRER_ADAPTER = ROOT / "UnityProject/Assets/Game/Platform/RuStore/RuStoreInstallReferrerService.cs"

verified_date = "2026-09-22"
expected = {
    "Pay": "11.1.0",
    "InstallReferrer": "10.6.1",
    "Update": "10.5.1",
    "Review": "10.5.1",
    "GameCenter": "10.5.2",
    "RemoteConfig": "10.5.1",
}
installed_packages = {
    "ru.rustore.pay": expected["Pay"],
    "ru.rustore.remoteconfig": expected["RemoteConfig"],
    "ru.rustore.update": expected["Update"],
    "ru.rustore.review": expected["Review"],
}

forbidden_unity_packages = {
    "ru.rustore.installreferrer": expected["InstallReferrer"],
}

versions_text = VERSIONS.read_text(encoding="utf-8")
matrix_text = MATRIX.read_text(encoding="utf-8")
checklist_text = CHECKLIST.read_text(encoding="utf-8")
readme_text = README.read_text(encoding="utf-8")
status_text = STATUS.read_text(encoding="utf-8")
native_referrer_text = NATIVE_REFERRER_DEPENDENCY.read_text(encoding="utf-8")
referrer_adapter_text = REFERRER_ADAPTER.read_text(encoding="utf-8")
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

for package, value in forbidden_unity_packages.items():
    if package in dependencies:
        errors.append(
            f"{package} {value} must not be installed as a Unity package beside Remote Config 10.5.1 because the verified official release artifacts contain duplicate .meta GUIDs."
        )

native_maven = "https://nexus-external.rustore.ru/repository/maven-rustore-exposed"
if constant("MavenRepository") != native_maven:
    errors.append("RuStore native Maven repository constant must match the current Install Referrer Android documentation.")
for marker in (
    'ru.rustore.sdk:installreferrer:10.6.1',
    native_maven,
):
    if marker not in native_referrer_text:
        errors.append(f"Native Install Referrer dependency contract is missing {marker!r}.")
for marker in (
    'ru.rustore.sdk.install.referrer.InstallReferrerClient',
    'ru.rustore.sdk.core.tasks.OnSuccessListener',
    'ru.rustore.sdk.core.tasks.OnFailureListener',
    '"getInstallReferrer"',
    '"getReferrerId"',
):
    if marker not in referrer_adapter_text:
        errors.append(f"Native Install Referrer Android bridge is missing {marker!r}.")

if not matrix_text.startswith(f"# RuStore SDK matrix — {verified_date}\n"):
    errors.append("RUSTORE_SDK_MATRIX.md verification date does not match RuStoreSdkVersions.LastVerifiedUtc.")

for name, value in expected.items():
    if value not in matrix_text:
        errors.append(f"RUSTORE_SDK_MATRIX.md does not document the verified {name} version {value}.")

checklist_requirements = {
    "Pay": f"Pay {expected['Pay']}",
    "InstallReferrer": f"Install Referrer Android {expected['InstallReferrer']}",
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
            f"Install Referrer Android `{expected['InstallReferrer']}`",
            f"Remote Config Unity `{expected['RemoteConfig']}`",
        ),
    ),
    "docs/MVP_STATUS.md": (
        status_text,
        (
            f"Последняя сверка RuStore targets на {verified_date}:",
            f"Pay Unity: `{expected['Pay']}`",
            f"Install Referrer Android: `{expected['InstallReferrer']}`",
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
    errors.append("Deprecated RuStore repository address detected in active Unity package configuration.")
if "nexus-external.rustore.ru" in manifest_text:
    errors.append("Native RuStore Maven repository must not replace the Unity npm scoped registry in Packages/manifest.json.")
if "ru.rustore.core" in dependencies:
    errors.append("ru.rustore.core must resolve transitively; do not pin it directly beside feature packages.")

if errors:
    raise SystemExit("RuStore SDK baseline validation failed:\n- " + "\n- ".join(errors))

print("RuStore SDK baseline validation passed.")
