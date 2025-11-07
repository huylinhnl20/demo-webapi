
using Microsoft.AspNetCore.Components.Forms;

namespace DemoWebApplication.Services
{
    public class OpenshiftFileWatcherService : IFileWatcherService, IDisposable
    {
        private readonly ILogger<OpenshiftFileWatcherService> _logger;
        private bool _disposed;
        private FileSystemWatcher? _watcher;
        private Action<string>? _onCertificateChanged;

        public OpenshiftFileWatcherService(ILogger<OpenshiftFileWatcherService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void SetCertificateChangedCallback(Action<string> onCertificateChanged)
        {
            _onCertificateChanged = onCertificateChanged;
        }

        public async Task StartWatchingAsync(string path, string filter = "*", CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpenshiftFileWatcherService));

            await StopWatchingAsync();

            var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                _logger.LogWarning("Directory does not exist: {Directory}", directory);
                return;
            }

            _logger.LogInformation("Starting file watcher for directory: {Directory}, filter: {Filter}", directory, filter);

            _watcher = new FileSystemWatcher(directory, filter)
            {
                NotifyFilter = NotifyFilters.LastWrite |
                                NotifyFilters.CreationTime |
                                NotifyFilters.Attributes,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            //_watcher.Renamed += OnFileRenamed;
            //_watcher.Error += OnError;

            await Task.CompletedTask;
        }

        public async Task StopWatchingAsync()
        {
            if (_watcher != null)
            {
                _logger.LogInformation("Stopping file watcher");

                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileChanged;
                _watcher.Created -= OnFileChanged;
                _watcher.Deleted -= OnFileChanged;
                //_watcher.Renamed -= OnFileRenamed;
                //_watcher.Error -= OnError;
                _watcher.Dispose();
                _watcher = null;
            }

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                StopWatchingAsync().Wait();
            }
        }


        protected virtual void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                _logger.LogInformation("Certificate file change detected: {FileName}, Type: {ChangeType}", 
                    e.Name, e.ChangeType);

                // Check if this is a certificate file
                if (e.FullPath.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) || 
                    e.FullPath.EndsWith(".p12", StringComparison.OrdinalIgnoreCase))
                {
                    // Wait a moment for the file to be completely written
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        try
                        {
                            _logger.LogInformation("Triggering certificate reload for: {FilePath}", e.FullPath);
                            _onCertificateChanged?.Invoke(e.FullPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error during certificate reload callback for: {FullPath}", e.FullPath);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file change event for: {FullPath}", e.FullPath);
            }
        }
    }
}
