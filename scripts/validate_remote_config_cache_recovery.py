#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "UnityProject/Assets/Game/Platform/RuStore/RuStoreRemoteConfigService.cs"
ASMDEF = ROOT / "UnityProject/Assets/Game/Platform/RuStore/Game.Platform.RuStore.asmdef"

source = SOURCE.read_text(encoding="utf-8")
asmdef = ASMDEF.read_text(encoding="utf-8")

required = {
    "using DontGetSidetracked.Core;": "Remote Config cache recovery must reuse the shared Core recovery policy.",
    'private readonly string _cacheBackupPath;': "Remote Config cache must keep a backup path.",
    'private readonly string _cacheTempPath;': "Remote Config cache must keep a temp path.",
    '_cacheBackupPath = _cachePath + ".bak";': "Remote Config backup path must be derived from the primary cache path.",
    '_cacheTempPath = _cachePath + ".tmp";': "Remote Config temp path must be derived from the primary cache path.",
    'Snapshot interruptedWrite = TryLoadCacheFile(_cacheTempPath);': "Startup must inspect an interrupted temp write.",
    'SaveRecoveryPolicy.ShouldRecoverInterruptedWrite': "Startup must reuse SaveRecoveryPolicy for temp-vs-primary selection.",
    'PromoteInterruptedCache(preservePrimaryAsBackup: primary != null);': "A recoverable temp snapshot must be promoted.",
    'Snapshot backup = TryLoadCacheFile(_cacheBackupPath);': "Startup must fall back to the previous valid backup.",
    'RestoreCacheFromBackup(overwriteExisting: true);': "Valid backup recovery must restore the primary cache file.",
    'File.Copy(_cachePath, _cacheBackupPath, true);': "Cache replacement must preserve the previous primary as backup.",
    'File.Move(tempPath, _cachePath);': "A completed temp write must become the primary cache atomically within one filesystem.",
    'RestoreCacheFromBackup(overwriteExisting: false);': "Failed cache replacement must restore backup when primary is missing.",
    'TryDelete(tempPath);': "Failed writes must remove stale temp files.",
    'private static Snapshot TryLoadCacheFile(string path)': "Each cache candidate must be parsed independently.",
    'private static void TryDelete(string path)': "Stale cache artifacts must be removed safely.",
}

errors = [message for needle, message in required.items() if needle not in source]

if '"Game.Core"' not in asmdef:
    errors.append("Game.Platform.RuStore asmdef must reference Game.Core for SaveRecoveryPolicy.")

if 'File.WriteAllText(_cachePath' in source:
    errors.append("Remote Config must never write the primary cache file directly; writes must go through temp replacement.")

if errors:
    raise SystemExit("Remote Config cache recovery validation failed:\n- " + "\n- ".join(errors))

print("Remote Config cache recovery validation passed.")
