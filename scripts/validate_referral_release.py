from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
UNITY = ROOT / "UnityProject"
MANIFEST = UNITY / "Assets/Plugins/Android/AndroidManifest.xml"
OFFLINE_API = UNITY / "Assets/Game/Social/OfflineGameApi.cs"
CAPTURE = UNITY / "Assets/Game/Presentation/InstallReferrerCapture.cs"
DEEPLINK = UNITY / "Assets/Game/Presentation/ReferralDeepLinkReceiver.cs"
REFERRER = UNITY / "Assets/Game/Platform/RuStore/RuStoreInstallReferrerService.cs"
UNITY_API = UNITY / "Assets/Game/Network/UnityGameApi.cs"

ANDROID_NS = "http://schemas.android.com/apk/res/android"
NAME = f"{{{ANDROID_NS}}}name"
SCHEME = f"{{{ANDROID_NS}}}scheme"
HOST = f"{{{ANDROID_NS}}}host"
EXPORTED = f"{{{ANDROID_NS}}}exported"

errors: list[str] = []

root = ET.fromstring(MANIFEST.read_text(encoding="utf-8"))
activities = root.findall("./application/activity")
unity = [a for a in activities if a.attrib.get(NAME) == "com.unity3d.player.UnityPlayerActivity"]
if len(unity) != 1:
    errors.append("Referral contract requires exactly one UnityPlayerActivity declaration in the custom manifest.")
else:
    activity = unity[0]
    if activity.attrib.get(EXPORTED) != "true":
        errors.append("UnityPlayerActivity must be exported so the challenge deeplink can enter the app.")

    valid_filter = False
    for intent in activity.findall("./intent-filter"):
        actions = {x.attrib.get(NAME) for x in intent.findall("./action")}
        categories = {x.attrib.get(NAME) for x in intent.findall("./category")}
        datas = intent.findall("./data")
        has_data = any(x.attrib.get(SCHEME) == "nesbeisya" and x.attrib.get(HOST) == "challenge" for x in datas)
        if ("android.intent.action.VIEW" in actions and
                "android.intent.category.DEFAULT" in categories and
                "android.intent.category.BROWSABLE" in categories and
                has_data):
            valid_filter = True
            break
    if not valid_filter:
        errors.append("UnityPlayerActivity challenge intent-filter must be VIEW + DEFAULT + BROWSABLE for nesbeisya://challenge.")

offline = OFFLINE_API.read_text(encoding="utf-8")
for value in [
    'return "nesbeisya://challenge/" + token.ToUpperInvariant();',
    '"https://www.rustore.ru/catalog/app/"',
    '"?referrerId="',
    'Uri.EscapeDataString(packageName)',
    'Uri.EscapeDataString(token.ToUpperInvariant())',
]:
    if value not in offline:
        errors.append(f"Offline share/referral link contract missing: {value}")

unity_api = UNITY_API.read_text(encoding="utf-8")
if "new OfflineGameApi(Application.identifier)" not in unity_api:
    errors.append("OfflineGameApi must receive Application.identifier so the RuStore install link targets the production package.")

capture = CAPTURE.read_text(encoding="utf-8")
required_capture = [
    "if (save.InstallReferrerConsumed) return;",
    "InstallReferrerResult result = await service.ConsumeInstallReferrerAsync();",
    "if (!result.RequestSucceeded) return;",
    "save = repository.Load();",
    "save.InstallReferrerConsumed = true;",
    "string.IsNullOrWhiteSpace(save.PendingReferralId)",
    "repository.Save(save);",
]
for value in required_capture:
    if value not in capture:
        errors.append(f"Install Referrer one-shot persistence contract missing: {value}")

if capture.find("if (!result.RequestSucceeded) return;") > capture.find("save.InstallReferrerConsumed = true;"):
    errors.append("Install Referrer must not be marked consumed after a failed SDK request.")

referrer = REFERRER.read_text(encoding="utf-8")
for value in [
    "new InstallReferrerResult(false, null)",
    'NativeClientClass = "ru.rustore.sdk.install.referrer.InstallReferrerClient"',
    '"getInstallReferrerV2"',
    '"getInstallReferrer"',
    '"addOnSuccessListener"',
    '"addOnFailureListener"',
    '"ru.rustore.sdk.core.tasks.OnSuccessListener"',
    '"ru.rustore.sdk.core.tasks.OnFailureListener"',
    "referrerId",
    "string.IsNullOrWhiteSpace(referrerId) ? null : referrerId",
]:
    if value not in referrer:
        errors.append(f"RuStore Install Referrer adapter contract missing: {value}")

deeplink = DEEPLINK.read_text(encoding="utf-8")
for value in [
    "Application.deepLinkActivated += OnDeepLinkActivated;",
    "Application.absoluteURL",
    "ReferralRuntimePolicy.TryNormalizeForCurrentRuntime",
    "string.Equals(save.PendingReferralId, normalized, StringComparison.Ordinal)",
]:
    if value not in deeplink:
        errors.append(f"Referral deeplink ingress contract missing: {value}")

if errors:
    raise SystemExit("Referral release contract validation failed:\n- " + "\n- ".join(errors))

print("Referral release contract validation passed.")
