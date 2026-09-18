namespace DontGetSidetracked.Platform.RuStore
{
    public static class RuStoreSdkVersions
    {
        // Re-verified against the official RuStore Unity documentation on this date.
        // A production SDK upgrade must update both the exact pins and this verification date.
        public const string LastVerifiedUtc = "2026-09-18";

        public const string Pay = "11.1.0";
        public const string InstallReferrer = "10.6.1";
        public const string Update = "10.5.1";
        public const string Review = "10.5.1";
        public const string GameCenter = "10.5.2";
        public const string RemoteConfig = "10.5.1";

        // RuStore Push is intentionally not part of the MVP: Daily reminders are local notifications.
        // Do not add a Push version/package without a fresh Unity-specific compatibility check.

        // Current official RuStore Unity registry re-verified against the Pay / Install Referrer /
        // Remote Config / Update / Review documentation on 2026-09-18.
        // Direct SDK packages are pinned in Packages/manifest.json; ru.rustore.core resolves transitively.
        public const string NpmRegistry = "https://nexus-external.vkteam.ru/repository/npm-unity-rustore-exposed/";
        public const string MavenRepository = "https://nexus-external.vkteam.ru/repository/maven-rustore-exposed";
    }
}
