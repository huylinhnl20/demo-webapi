namespace DemoWebApplication.Services
{
    public interface IFileWatcherService
    {
        Task StartWatchingAsync(string path, string filter = "*", CancellationToken cancellationToken = default);
        Task StopWatchingAsync();
        void SetCertificateChangedCallback(Action<string> onCertificateChanged);
    }
}
