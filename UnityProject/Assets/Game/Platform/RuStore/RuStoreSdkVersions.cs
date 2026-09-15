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
        public const string Push = "7.4.0";

        // UPM/npm registry from the current Unity SDK installation guides.
        public const string NpmRegistry = "https://nexus-external.vkteam.ru/repository/npm-unity-rustore-exposed/";

        // Maven/Gradle infrastructure is migrating separately; verify generated EDM/Gradle files before release.
        public const string MavenRepositoryMigrationTarget = "https://nexus-external.rustore.ru/repository/maven-rustore-exposed";
    }
}
