namespace DontGetSidetracked.Platform.RuStore
{
    public static class RuStoreSdkVersions
    {
        public const string Pay = "11.1.0";
        public const string InstallReferrer = "10.6.0";
        public const string Update = "10.5.1";
        public const string Review = "10.5.1";
        public const string GameCenter = "10.5.2";
        public const string RemoteConfig = "10.5.0";

        // Verified against the official Unity Push documentation on 2026-09-15.
        // Do not confuse this with the Kotlin/Java Push SDK 7.4.0 branch.
        public const string Push = "6.3.0";

        // Current project registry. Re-check against official RuStore docs before production release.
        public const string NpmRegistry = "https://nexus-external.rustore.ru/repository/npm-unity-rustore-exposed/";
        public const string MavenRepository = "https://nexus-external.rustore.ru/repository/maven-rustore-exposed";
    }
}
