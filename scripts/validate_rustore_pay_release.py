from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VALIDATOR = ROOT / "UnityProject/Assets/Game/Editor/RuStorePayReleaseContractValidator.cs"
MANIFEST = ROOT / "UnityProject/Assets/Plugins/Android/AndroidManifest.xml"

if not VALIDATOR.is_file():
    raise SystemExit("RuStore Pay production preflight validator is missing.")

text = VALIDATOR.read_text(encoding="utf-8")
required = [
    'AssetDatabase.FindAssets("PayClientSettings t:ScriptableObject")',
    'serialized.FindProperty("consoleApplicationId")',
    'serialized.FindProperty("deeplinkScheme")',
    'serialized.FindProperty("schemeVersion")',
    'BuildOptions.Development',
    'com.unity3d.player.UnityPlayerActivity',
    '@string/rustore_PayClientSettings_deeplinkScheme',
    'console_app_id_value',
    'internal_config_key',
    'sdk_pay_scheme_value',
    'ru.rustore.unitysdk.RuStoreDeeplinkActivityDefault',
    'ru.rustore.unitysdk.RuStoreIntentFilterActivity',
]
missing = [item for item in required if item not in text]
if missing:
    raise SystemExit("RuStore Pay release preflight is incomplete: " + ", ".join(missing))

manifest = MANIFEST.read_text(encoding="utf-8")
if 'com.unity3d.player.UnityPlayerActivity' not in manifest:
    raise SystemExit("Android manifest lost UnityPlayerActivity required by RuStore Pay.")
if 'android:scheme="nesbeisya"' not in manifest or 'android:host="challenge"' not in manifest:
    raise SystemExit("Gameplay challenge deeplink contract is missing from Android manifest.")

for token, error in (
    ('@string/rustore_PayClientSettings_deeplinkScheme', "RuStore Pay generated deeplink resource reference is missing from Android manifest."),
    ('console_app_id_value', "RuStore Pay console_app_id_value meta-data is missing from Android manifest."),
    ('internal_config_key', "RuStore Pay internal_config_key meta-data is missing from Android manifest."),
    ('sdk_pay_scheme_value', "RuStore Pay sdk_pay_scheme_value meta-data is missing from Android manifest."),
    ('ru.rustore.unitysdk.RuStoreDeeplinkActivityDefault', "RuStore Pay deeplink Activity is missing from Android manifest."),
):
    if token not in manifest:
        raise SystemExit(error)

# Do not hardcode a RuStore Console application id or generated Pay scheme in the repository.
# The production-only Unity preflight above requires the official PayClientSettings asset and
# patched manifest before a non-development Android build can be produced.
if 'console_app_id_value" android:value="' in manifest and '@string/rustore_PayClientSettings_consoleApplicationId' not in manifest:
    raise SystemExit("RuStore Pay console application id must come from generated PayClientSettings resources, not a hardcoded manifest value.")

print("RuStore Pay release contract validation passed.")
