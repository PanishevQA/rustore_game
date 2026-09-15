#!/usr/bin/env python3
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
UNITY = ROOT / "UnityProject"
ERRORS: list[str] = []


def fail(message: str) -> None:
    ERRORS.append(message)


def read(path: pathlib.Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except FileNotFoundError:
        fail(f"Missing required file: {path.relative_to(ROOT)}")
        return ""


def validate_project_version() -> None:
    text = read(UNITY / "ProjectSettings/ProjectVersion.txt")
    if "m_EditorVersion: 6000.3.24f1" not in text:
        fail("Unity editor pin must remain 6000.3.24f1 until an explicit regression-tested upgrade.")


def validate_package_manifest() -> None:
    path = UNITY / "Packages/manifest.json"
    try:
        manifest = json.loads(read(path))
    except json.JSONDecodeError as exc:
        fail(f"Packages/manifest.json is invalid JSON: {exc}")
        return

    dependencies = manifest.get("dependencies") or {}
    expected = {
        "com.unity.mobile.notifications": "2.4.3",
        "ru.rustore.pay": "11.1.0",
        "ru.rustore.installreferrer": "10.6.1",
        "ru.rustore.update": "10.5.1",
        "ru.rustore.review": "10.5.1",
        "ru.rustore.remoteconfig": "10.5.1",
    }
    for package, version in expected.items():
        actual = dependencies.get(package)
        if actual != version:
            fail(f"{package} must be pinned to {version}; found {actual!r}.")

    registries = manifest.get("scopedRegistries") or []
    if not any(
        entry.get("url") == "https://nexus-external.rustore.ru/repository/npm-unity-rustore-exposed/"
        and "ru.rustore" in (entry.get("scopes") or [])
        for entry in registries
    ):
        fail("Current RuStore scoped npm registry is missing from Packages/manifest.json.")


def validate_asmdefs() -> None:
    files = sorted((UNITY / "Assets/Game").rglob("*.asmdef"))
    names: dict[str, pathlib.Path] = {}
    parsed: list[tuple[pathlib.Path, dict]] = []

    for path in files:
        try:
            data = json.loads(read(path))
        except json.JSONDecodeError as exc:
            fail(f"Invalid asmdef JSON in {path.relative_to(ROOT)}: {exc}")
            continue
        name = data.get("name")
        if not isinstance(name, str) or not name.strip():
            fail(f"Assembly name is missing in {path.relative_to(ROOT)}")
            continue
        if name in names:
            fail(
                f"Duplicate assembly name {name!r}: "
                f"{names[name].relative_to(ROOT)} and {path.relative_to(ROOT)}"
            )
        names[name] = path
        parsed.append((path, data))

    for path, data in parsed:
        for reference in data.get("references") or []:
            # GUID refs and package/Unity assemblies are resolved by Unity/UPM. Internal Game.* refs must exist here.
            if isinstance(reference, str) and reference.startswith("Game.") and reference not in names:
                fail(f"{path.relative_to(ROOT)} references missing internal assembly {reference!r}.")


def validate_android_manifest() -> None:
    text = read(UNITY / "Assets/Plugins/Android/AndroidManifest.xml")
    required = (
        "com.unity3d.player.UnityPlayerActivity",
        'android:scheme="nesbeisya"',
        'android:host="challenge"',
    )
    for token in required:
        if token not in text:
            fail(f"AndroidManifest.xml is missing {token!r}.")
    if "GameActivity" in text:
        fail("AndroidManifest.xml must not use GameActivity; RuStore Pay requires UnityPlayerActivity.")


def validate_required_runtime_files() -> None:
    paths = (
        "Assets/Game/Presentation/GameBootstrap.cs",
        "Assets/Game/Presentation/MobileUiCoordinator.cs",
        "Assets/Game/Social/OfflineGameApi.cs",
        "Assets/Game/Platform/RuStore/RuStorePaymentService.cs",
        "Assets/Game/Platform/RuStore/RuStoreInstallReferrerService.cs",
        "Assets/Game/Platform/RuStore/RuStoreRemoteConfigService.cs",
        "Assets/Game/Platform/Android/LocalDailyNotificationScheduler.cs",
        "Assets/Game/Editor/ProductionReleaseValidator.cs",
    )
    for relative in paths:
        if not (UNITY / relative).is_file():
            fail(f"Missing required runtime file: UnityProject/{relative}")


def validate_source_hygiene() -> None:
    for path in (UNITY / "Assets/Game").rglob("*.cs"):
        text = read(path)
        if "<<<<<<<" in text or "=======" in text or ">>>>>>>" in text:
            fail(f"Merge conflict marker found in {path.relative_to(ROOT)}")


def main() -> int:
    validate_project_version()
    validate_package_manifest()
    validate_asmdefs()
    validate_android_manifest()
    validate_required_runtime_files()
    validate_source_hygiene()

    if ERRORS:
        print("Unity project static smoke-check FAILED:")
        for error in ERRORS:
            print(f"- {error}")
        return 1

    print("Unity project static smoke-check passed.")
    print("Note: this does not replace opening/compiling the project in Unity or Android device tests.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
