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
    expected = {
        "com.unity.test-framework": "1.6.0",
        "com.unity.mobile.notifications": "2.4.3",
        "com.google.external-dependency-manager": "https://github.com/googlesamples/unity-jar-resolver.git?path=/upm#v1.2.188",
        "com.yandex.mobileads": "https://github.com/yandexmobile/yandex-ads-unity-plugin.git?path=/mobileads-sdk#8.4.0",
        "com.unity.modules.audio": "1.0.0",
        "com.unity.modules.imageconversion": "1.0.0",
        "ru.rustore.pay": "11.1.0",
        "ru.rustore.update": "10.5.1",
        "ru.rustore.review": "10.5.1",
    }
    for package, version in expected.items():
        actual = dependencies.get(package)
        if actual != version:
            fail(f"{package} must be pinned to {version}; found {actual!r}.")

    # RuStore documentation states that ru.rustore.core is installed transitively by each
    # feature package. Do not pin a potentially incompatible core version directly.
    if "ru.rustore.core" in dependencies:
        fail("ru.rustore.core must resolve transitively; remove the direct core pin from Packages/manifest.json.")

    for quarantined in ("ru.rustore.installreferrer", "ru.rustore.remoteconfig"):
        if quarantined in dependencies:
            fail(f"{quarantined} must stay out of the Unity 6000.3.24f1 Editor baseline until a fixed official/source integration is re-verified.")

    registries = manifest.get("scopedRegistries") or []
    expected_registry = "https://nexus-external.vkteam.ru/repository/npm-unity-rustore-exposed/"
    if not any(
        entry.get("url") == expected_registry and "ru.rustore" in (entry.get("scopes") or [])
        for entry in registries
    ):
        fail("Current official RuStore scoped npm registry is missing from Packages/manifest.json.")

    if not any(
        entry.get("url") == "https://package.openupm.com" and "com.yandex" in (entry.get("scopes") or [])
        for entry in registries
    ):
        fail("OpenUPM registry for Yandex package dependencies is missing from Packages/manifest.json.")

    serialized = json.dumps(manifest, ensure_ascii=False)
    if "nexus-external.rustore.ru" in serialized or "artifactory-external.vkpartner.ru" in serialized:
        fail("Packages/manifest.json contains an obsolete RuStore repository address.")


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
            fail(f"Duplicate assembly name {name!r}: {names[name].relative_to(ROOT)} and {path.relative_to(ROOT)}")
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

    permissions = {item.attrib.get(ANDROID_NAME, "") for item in root.findall("./uses-permission")}
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

    application = root.find("./application")
    if application is None:
        fail("AndroidManifest.xml must declare an application element.")
        return
    activities = list(root.findall("./application/activity"))
    unity_activities = [a for a in activities if a.attrib.get(ANDROID_NAME) == "com.unity3d.player.UnityPlayerActivity"]
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
    file_providers = [p for p in providers if p.attrib.get(ANDROID_NAME) == "androidx.core.content.FileProvider"]
    valid_share_provider = False
    for provider in file_providers:
        if provider.attrib.get(ANDROID_AUTHORITIES) != "${applicationId}.shareprovider":
            continue
        if provider.attrib.get(ANDROID_EXPORTED) != "false":
            continue
        if provider.attrib.get(ANDROID_GRANT_URI_PERMISSIONS) != "true":
            continue
        for meta in provider.findall("./meta-data"):
            if meta.attrib.get(ANDROID_NAME) == "android.support.FILE_PROVIDER_PATHS" and meta.attrib.get(ANDROID_RESOURCE) == "@xml/nesbeisya_file_paths":
                valid_share_provider = True
                break
        if valid_share_provider:
            break
    if not valid_share_provider:
        fail("Result PNG sharing requires a non-exported ${applicationId}.shareprovider FileProvider with nesbeisya_file_paths.")


def validate_share_path_alignment() -> None:
    xml_path = UNITY / "Assets/ResultShare.androidlib/src/main/res/xml/nesbeisya_file_paths.xml"
    try:
        root = ET.fromstring(read(xml_path))
    except ET.ParseError as exc:
        fail(f"nesbeisya_file_paths.xml is invalid XML: {exc}")
        return

    cache_paths = root.findall("./cache-path")
    if not any(item.attrib.get("path") == "share/" for item in cache_paths):
        fail("FileProvider must expose exactly the share/ cache subdirectory used by NativeImageShare.")

    share_code = read(UNITY / "Assets/Game/Presentation/NativeImageShare.cs")
    if 'Path.Combine(Application.temporaryCachePath, "share")' not in share_code:
        fail("NativeImageShare must write PNG cards into the FileProvider share/ cache subdirectory.")
    if 'Application.identifier + ".shareprovider"' not in share_code:
        fail("NativeImageShare authority must match ${applicationId}.shareprovider from AndroidManifest.")


def validate_local_notification_contract() -> None:
    text = read(UNITY / "Assets/Game/Platform/Android/LocalDailyNotificationScheduler.cs")
    required = {
        "RepeatInterval = TimeSpan.FromDays(1)": "Daily reminder must continue locally across missed app launches.",
        "ShowInForeground = false": "Daily reminder must not be shown over active gameplay.",
        "CancelScheduledNotification(NotificationId)": "Daily reminder rescheduling must replace the previous schedule instead of duplicating notifications.",
    }
    for needle, message in required.items():
        if needle not in text:
            fail(message)


def validate_required_runtime_files() -> None:
    paths = (
        "Assets/Game/Presentation/GameBootstrap.cs",
        "Assets/Game/Presentation/MobileUiCoordinator.cs",
        "Assets/Game/Presentation/HomeDashboardCoordinator.cs",
        "Assets/Game/Presentation/GameplayGridCoordinator.cs",
        "Assets/Game/Presentation/ReleaseUiKit.cs",
        "Assets/Game/Presentation/ReleasePanelMotion.cs",
        "Assets/Game/Presentation/GameplayHudCoordinator.cs",
        "Assets/Game/Presentation/ReferralOfferCoordinator.cs",
        "Assets/Game/Presentation/NativeImageShare.cs",
        "Assets/Game/Social/OfflineGameApi.cs",
        "Assets/Game/Platform/RuStore/RuStorePaymentService.cs",
        "Assets/Game/Platform/RuStore/RuStoreInstallReferrerService.cs",
        "Assets/Game/Platform/RuStore/RuStoreRemoteConfigService.cs",
        "Assets/Game/Platform/RuStore/RuStoreRemoteConfigRuntime.cs",
        "Assets/Game/Platform/RuStore/RuStoreReviewService.cs",
        "Assets/Game/Platform/RuStore/RuStoreUpdateService.cs",
        "Assets/Game/Platform/Android/LocalDailyNotificationScheduler.cs",
        "Assets/Game/Editor/ProjectConfigurator.cs",
        "Assets/Game/Editor/AndroidDependencyConfigurator.cs",
        "Assets/Game/Editor/BrandAssetConfigurator.cs",
        "Assets/Game/Editor/ReleaseReadinessReporter.cs",
        "Assets/Game/Editor/RuStorePayProductionConfigurator.cs",
        "Assets/Game/Editor/ProductionReleaseValidator.cs",
        "Assets/ResultShare.androidlib/src/main/res/xml/nesbeisya_file_paths.xml",
    )
    for relative in paths:
        if not (UNITY / relative).is_file():
            fail(f"Missing required runtime file: UnityProject/{relative}")


def validate_remote_config_runtime_contract() -> None:
    runtime = read(UNITY / "Assets/Game/Platform/RuStore/RuStoreRemoteConfigRuntime.cs")
    bootstrap = read(UNITY / "Assets/Game/Network/BootstrapRemoteConfigService.cs")
    tuning = read(UNITY / "Assets/Game/Presentation/RemoteGameplayTuningCoordinator.cs")

    if "RuntimeInitializeLoadType.SubsystemRegistration" not in runtime or "_service = null" not in runtime:
        fail("Shared RuStore Remote Config runtime must reset static state between Unity Play sessions.")
    if "RuStoreRemoteConfigRuntime.Service" not in bootstrap:
        fail("Compatibility Remote Config facade must use the centralized RuStore runtime provider.")
    if "RuStoreRemoteConfigRuntime.Service" not in tuning:
        fail("Gameplay tuning must use the centralized RuStore runtime provider.")
    if "DontGetSidetracked.Network" in tuning or "BootstrapRemoteConfigService" in tuning:
        fail("Gameplay Remote Config tuning must not depend on the optional Network module.")


def validate_live_stroke_contract() -> None:
    bootstrap = read(UNITY / "Assets/Game/Presentation/GameBootstrap.cs")
    graphic = read(UNITY / "Assets/Game/Presentation/RouteGraphic.cs")
    if "_playerGraphic.AppendPoint(point);" not in bootstrap:
        fail("Live drawing must append directly to RouteGraphic without rebuilding a temporary position list.")
    if "new List<FixedPoint2>(_recording.Count)" in bootstrap:
        fail("Per-touch-sample full position list allocation returned to GameBootstrap.")
    if "public void AppendPoint(FixedPoint2 point)" not in graphic:
        fail("RouteGraphic must expose the allocation-free live AppendPoint contract.")


def validate_release_preflight_contract() -> None:
    text = read(UNITY / "Assets/Game/Editor/ProductionReleaseValidator.cs")
    required = {
        r'\"ru.rustore.pay\": \"11.1.0\"': "Production preflight must enforce RuStore Pay 11.1.0.",
        'ValidateQuarantinedRuStorePackages(errors)': "Production preflight must keep known-broken RuStore Editor packages quarantined.",
        'HasLoadedRuStoreType("InstallReferrerClient")': "Production preflight must require an actually loaded Install Referrer Unity integration.",
        'HasLoadedRuStoreType("RuStoreRemoteConfigClient")': "Production preflight must require an actually loaded Remote Config Unity integration.",
        'InstallReferrer = \\"10.6.1\\"': "Production preflight must protect the current Install Referrer target version.",
        'RemoteConfig = \\"10.5.1\\"': "Production preflight must protect the current Remote Config target version.",
        'android.permission.POST_NOTIFICATIONS': "Production preflight must protect the Daily reminder permission.",
        'androidx.core.content.FileProvider': "Production preflight must protect result-card FileProvider wiring.",
        'ValidateForbiddenManifestPermissions': "Production preflight must reject unnecessary sensitive permissions.",
        'yandexmobile/yandex-ads-unity-plugin.git?path=/mobileads-sdk#8.4.0': "Production preflight must require the pinned Yandex package.",
        'ValidateBranding(errors)': "Production preflight must validate launcher branding.",
        'useCustomKeystore': "Production preflight must require custom signing.",
        'buildAppBundle': "Production preflight must require AAB output.",
        'ScriptingImplementation.IL2CPP': "Production preflight must require IL2CPP.",
        'AndroidArchitecture.ARM64': "Production preflight must require ARM64.",
    }
    for needle, message in required.items():
        if needle not in text:
            fail(message)


def validate_portrait_game_view_batchmode_guard() -> None:
    text = read(UNITY / "Assets/Game/Editor/PortraitGameViewConfigurator.cs")
    if text.count("Application.isBatchMode") < 2:
        fail("Portrait Game View configurator must not open Editor windows during Unity batchmode checks.")


def validate_local_release_candidate_runner() -> None:
    path = ROOT / "scripts/run_release_candidate_checks.ps1"
    text = read(path)
    required = (
        "ProjectSettings\\ProjectVersion.txt",
        '"ru.rustore.installreferrer"',
        '"ru.rustore.remoteconfig"',
        "Stale quarantined RuStore package state detected",
        "Start-Process -FilePath $unity",
        "-Wait -PassThru",
        ".ExitCode",
        "-runTests",
        "-testPlatform EditMode",
        "DontGetSidetracked.EditorTools.ReleaseReadinessReporter.Report",
        "release-readiness.txt",
    )
    for marker in required:
        if marker not in text:
            fail(f"Local release-candidate runner is incomplete: missing {marker!r}.")


def validate_production_identifiers() -> None:
    project = read(UNITY / "Assets/Game/Editor/ProjectConfigurator.cs")
    remote = read(UNITY / "Assets/Game/Platform/RuStore/RuStoreRemoteConfigService.cs")
    ads = read(UNITY / "Assets/Game/Monetization/YandexMobileAdsService.cs")
    pay = read(UNITY / "Assets/Game/Editor/RuStorePayProductionConfigurator.cs")

    required = (
        (project, 'ProductionPackageName = "ru.release.nesbeisya"', "Final Android package name"),
        (project, 'ProductionKeyAlias = "nesbeysya"', "Production signing alias"),
        (remote, 'AppId = "4e0feafb-1ce7-4b71-966b-0122938b282a"', "RuStore Remote Config AppId"),
        (ads, 'RewardedUnitId = "R-M-20071218-1"', "Yandex rewarded production ID"),
        (ads, 'InterstitialUnitId = "R-M-20071218-2"', "Yandex interstitial production ID"),
        (pay, 'ConsoleApplicationId = "2063758837"', "RuStore Pay console application ID"),
        (pay, 'DeeplinkScheme = "nesbeisyapay"', "RuStore Pay deeplink scheme"),
    )
    for text, marker, label in required:
        if marker not in text:
            fail(f"{label} production contract is missing: {marker!r}.")


def validate_release_readiness_reporter() -> None:
    text = read(UNITY / "Assets/Game/Editor/ReleaseReadinessReporter.cs")
    required = (
        "ProjectConfigurator.Configure()",
        "AndroidDependencyConfigurator.Configure()",
        "BrandAssetConfigurator.Configure()",
        "AndroidDependencyConfigurator.ForceResolveAndroidDependencies()",
        "EditorUserBuildSettings.buildAppBundle = true",
        "ProductionReleaseVersionValidator.CollectErrors()",
        "ProductionPlaceholderValidator.CollectErrors()",
        "ProductionReleaseValidator.CollectErrors()",
        "RuStorePayReleaseContractValidator.CollectErrors()",
        "READY FOR SIGNED ANDROID DEVICE SMOKE TEST",
    )
    for marker in required:
        if marker not in text:
            fail(f"Release readiness reporter is incomplete: missing {marker!r}.")


def validate_android_dependency_configurator() -> None:
    text = read(UNITY / "Assets/Game/Editor/AndroidDependencyConfigurator.cs")
    required = {
        'FindType("GooglePlayServices.PlayServicesResolver")': "Android dependency configurator must locate EDM4U without a hard assembly dependency.",
        '"ResolveSync"': "Android dependency configurator must use EDM4U synchronous resolution.",
        'new[] { typeof(bool) }': "EDM4U ResolveSync(bool) signature must stay explicit.",
        'new object[] { true }': "Production dependency resolution must be forced, not opportunistic.",
        "EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android": "EDM4U resolution must reject non-Android active targets.",
    }
    for needle, message in required.items():
        if needle not in text:
            fail(message)


def validate_editor_configuration_safety() -> None:
    text = read(UNITY / "Assets/Game/Editor/ProjectConfigurator.cs")
    required = (
        "DevelopmentPackageName",
        "PlayerSettings.GetApplicationIdentifier",
        "ShouldAssignDevelopmentPackageName",
        "if (ShouldAssignDevelopmentPackageName(currentPackage))",
    )
    for needle in required:
        if needle not in text:
            fail("ProjectConfigurator must preserve an explicitly configured production Android package name.")
            break


def validate_repository_hygiene() -> None:
    text = read(ROOT / ".gitignore")
    required = (
        "*.apk", "*.aab", "*.keystore", "*.jks", "*.p12", "*.pfx", "*.pem",
        "keystore.properties", "local.properties", ".env",
    )
    missing = [entry for entry in required if entry not in text]
    if missing:
        fail(".gitignore is missing release-artifact/signing protections: " + ", ".join(missing))


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
    validate_share_path_alignment()
    validate_local_notification_contract()
    validate_required_runtime_files()
    validate_remote_config_runtime_contract()
    validate_live_stroke_contract()
    validate_release_preflight_contract()
    validate_production_identifiers()
    validate_portrait_game_view_batchmode_guard()
    validate_local_release_candidate_runner()
    validate_release_readiness_reporter()
    validate_android_dependency_configurator()
    validate_editor_configuration_safety()
    validate_repository_hygiene()
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