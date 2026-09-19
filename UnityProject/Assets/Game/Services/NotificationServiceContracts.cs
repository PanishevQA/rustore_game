namespace DontGetSidetracked.Services
{
    public interface INotificationPermissionService
    {
        bool IsRuntimePermissionRequired { get; }
        bool IsGranted { get; }
        void Request();
    }
}
