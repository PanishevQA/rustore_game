namespace DontGetSidetracked.Presentation
{
    public static class GameRuntimeSettings
    {
        // Offline-first MVP: no developer-operated backend is required.
        // A future online build may set this to an HTTPS API and the existing facade will use it.
        public const string OptionalBackendBaseUrl = "";

        public static string BackendBaseUrl => OptionalBackendBaseUrl;
        public static bool UsesDeveloperBackend => !string.IsNullOrWhiteSpace(OptionalBackendBaseUrl);
    }
}
