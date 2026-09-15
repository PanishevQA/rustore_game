namespace DontGetSidetracked.Presentation
{
    public static class GameRuntimeSettings
    {
        // Set this to the deployed HTTPS API before making a production Android build.
        public const string ProductionBackendBaseUrl = "";

        public static string BackendBaseUrl
        {
            get
            {
#if UNITY_EDITOR
                return "http://127.0.0.1:8000";
#else
                return ProductionBackendBaseUrl;
#endif
            }
        }
    }
}
