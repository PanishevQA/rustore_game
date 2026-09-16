#!/usr/bin/env python3
import json
import pathlib
import sys
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]
UNITY = ROOT / "UnityProject"
ERRORS: list[str] = []
ANDROID_NS = "http://schemas.android.com/apk/res/android"
ANDROID_NAME = f"{{{ANDROID_NS}}}name"
ANDROID_SCHEME = f"{{{ANDROID_NS}}}scheme"
ANDROID_HOST = f"{{{ANDROID_NS}}}host"
ANDROID_EXPORTED = f"{{{ANDROID_NS}}}exported"
ANDROID_AUTHORITIES = f"{{{ANDROID_NS}}}authorities"
ANDROID_GRANT_URI_PERMISSIONS = f"{{{ANDROID_NS}}}grantUriPermissions"
ANDROID_RESOURCE = f"{{{ANDROID_NS}}}resource"


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
    # Only packages required for the editor-functional MVP are pinned here.
    # Install Referrer and Remote Config are intentionally omitted from the editor manifest:
    # their SDK adapters are reflection-based and they are added later for Android/RuStore
    # integration testing using the official RuStore tarballs/packages.
    expected = {
        "com.unity.test-framework": "1.6.0",
        "com.unity.mobile.notifications": "2.4.3",
        "com.unity.modules.audio": "1.0.0",
        "com.unity.modules.imageconversion": "1.0.0",
        "ru.rustore.core": "10.5.0",
        "ru.rustore.pay": "11.1.0",
        "ru.rustore.update": "10.5.1",
        "ru.rustore.review": "10.5.1",
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
            if isinstance(reference, str) and reference.startswith("Game.") and reference not in names:
                fail(f"{path.relative_to(ROOT)} references missing internal assembly {reference!r}.")


def validate_android_manifest() -> None:
    path = UNITY / "Assets/Plugins/Android/AndroidManifest.xml"
    text = read(path)
    if not text:
        return

    try:
        root = ET.fromstring(text)
    except ET.ParseError as exc:
        fail(f"AndroidManifest.xml is invalid XML: {exc}")
        return

    permissions = {
        item.attrib.get(ANDROID_NAME, "")
        for item in root.findall("./uses-permission")
    }
    if "android.permission.POST_NOTIFICATIONS" not in permissions:
        fail("AndroidManifest.xml must declare POST_NOTIFICATIONS for contextual Daily reminders on Android 13+.")

    forbidden_permissions = {
        "android.permission.ACCESS_FINE_LOCATION",
        "android.permission.ACCESS_COARSE_LOCATION",
        "android.permission.READ_CONTACTS",
        "android.permission.WRITE_CONTACTS",
        "android.permission.RECORD_AUDIO",
        "android.permission.CAMERA",
        "android.permission.READ_SMS",
        "android.permission.SEND_SMS",
        "android.permission.READ_EXTERNAL_STORAGE",
        "android.permission.WRITE_EXTERNAL_STORAGE",
        "android.permission.MANAGE_EXTERNAL_STORAGE",
    }
    forbidden_found = sorted(permission for permission in permissions if permission in forbidden_permissions)
    if forbidden_found:
        fail("Unnecessary sensitive Android permissions declared: " + ", ".join(forbidden_found))

    activities = list(root.findall("./application/activity"))
    unity_activities = [
        activity for activity in activities
        if activity.attrib.get(ANDROID_NAME) == "com.unity3d.player.UnityPlayerActivity"
    ]
    if not unity_activities:
        fail("AndroidManifest.xml must declare com.unity3d.player.UnityPlayerActivity.")

    for activity in activities:
        actual_name = activity.attrib.get(ANDROID_NAME, "")
        if actual_name.endswith("GameActivity"):
            fail(f"AndroidManifest.xml must not declare GameActivity ({actual_name}); RuStore Pay requires UnityPlayerActivity.")

    has_challenge_deeplink = False
    for activity in unity_activities:
        for data in activity.findall("./intent-filter/data"):
            if data.attrib.get(ANDROID_SCHEME) == "nesbeisya" and data.attrib.get(ANDROID_HOST) == "challenge":
                has_challenge_deeplink = True
                break
        if has_challenge_deeplink:
            break
    if not has_challenge_deeplink:
        fail("UnityPlayerActivity must declare the nesbeisya://challenge deeplink.")

    providers = list(root.findall("./application/provider"))
    file_providers = [
        provider for provider in providers
        if provider.attrib.get(ANDROID_NAME) == "androidx.core.content.FileProvider"
    ]
    valid_share_provider = False
    for provider in file_providers:
        if provider.attrib.get(ANDROID_AUTHORITIES) != "${applicationId}.shareprovider":
            continue
        if provider.attrib.get(ANDROID_EXPORTED) != "false":
            continue
        if provider.attrib.get(ANDROID_GRANT_URI_PERMISSIONS) != "true":
            continue
        for meta in provider.findall("./meta-data"):
            if (
                meta.attrib.get(ANDROID_NAME) == "android.support.FILE_PROVIDER_PATHS"
                and meta.attrib.get(ANDROID_RESOURCE) == "@xml/nesbeisya_file_paths"
            ):
                valid_share_provider = True
                break
        if valid_share_provider:
            break
    if not valid_share_provider:
        fail("Result PNG sharing requires a non-exported ${applicationId}.shareprovider FileProvider with nesbeisya_file_paths.")


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
        "Assets/ResultShare.androidlib/src/main/res/xml/nesbeisya_file_paths.xml",
    )
    for relative in paths:
        if not (UNITY / relative).is_file():
            fail(f"Missing required runtime file: UnityProject/{relative}")


def validate_release_preflight_contract() -> None:
    path = UNITY / "Assets/Game/Editor/ProductionReleaseValidator.cs"
    text = read(path)
    required = {
        r'\"ru.rustore.pay\": \"11.1.0\"': "Production preflight must enforce RuStore Pay 11.1.0.",
        r'\"ru.rustore.installreferrer\": \"10.6.1\"': "Production preflight must block release until Install Referrer 10.6.1 is restored.",
        r'\"ru.rustore.remoteconfig\": \"10.5.0\"': "Production preflight must block release until Remote Config 10.5.0 is restored.",
        'YANDEX_MOBILE_ADS': "Production preflight must require the Yandex ads integration symbol.",
        'useCustomKeystore': "Production preflight must require custom signing.",
        'buildAppBundle': "Production preflight must require AAB output.",
        'ScriptingImplementation.IL2CPP': "Production preflight must require IL2CPP.",
        'AndroidArchitecture.ARM64': "Production preflight must require ARM64.",
    }
    for needle, message in required.items():
        if needle not in text:
            fail(message)


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
    validate_release_preflight_contract()
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
