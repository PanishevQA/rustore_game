namespace DontGetSidetracked.Platform.RuStore
{
    public static class RuStoreSdkVersions
    {
        public const string Pay = "11.1.0";
        public const string InstallReferrer = "10.6.1";
        public const string Update = "10.5.1";
        public const string Review = "10.5.1";
        public const string GameCenter = "10.5.2";
        public const string RemoteConfig = "10.5.1";

        // RuStore Push is intentionally not part of the MVP: Daily reminders are local notifications.
        // Do not add a Push version/package without a fresh Unity-specific compatibility check.

        // Current project registry. Install Referrer/Remote Config remain optional Editor integrations and
        // must be re-verified from official packages before production Android builds.
        public const string NpmRegistry = "https://nexus-external.rustore.ru/repository/npm-unity-rustore-exposed/";
        public const string MavenRepository = "https://nexus-external.rustore.ru/repository/maven-rustore-exposed";
    }
}
