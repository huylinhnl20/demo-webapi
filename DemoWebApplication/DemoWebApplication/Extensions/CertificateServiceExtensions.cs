using System.Security.Cryptography.X509Certificates;
using DemoWebApplication.Services;

namespace DemoWebApplication.Extensions;

public static class CertificateServiceExtensions
{
    private static X509Certificate2? _currentCertificate;
    private static readonly object _certificateLock = new object();
    private static ILogger? _logger;

    public static IServiceCollection AddCertificateServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    { 
        services.AddSingleton<IFileWatcherService, OpenshiftFileWatcherService>();
        return services;
    }

    public static WebApplicationBuilder ConfigureKestrelWithCertificateSelector(
        this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var certPath = configuration["Certificate:Path"] ?? "/app/certs/server.pfx";
        var certPassword = configuration["Certificate:Password"] ?? "";

        // Initialize logger for certificate operations
        var loggerFactory = builder.Services.BuildServiceProvider().GetService<ILoggerFactory>();
        _logger = loggerFactory?.CreateLogger(typeof(CertificateServiceExtensions));

        // Load initial certificate
        LoadCertificate(certPath, certPassword);

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.ConfigureHttpsDefaults(listenOptions =>
            {
                listenOptions.ServerCertificateSelector = (context, name) =>
                {
                    lock (_certificateLock)
                    {
                        _logger?.LogDebug("Serving certificate for name: {Name}", name);
                        return _currentCertificate;
                    }
                };
            });
        });

        return builder;
    }

    public static void ReloadCertificate(string certPath, string certPassword = "")
    {
        try
        {
            _logger?.LogInformation("Reloading certificate from: {CertPath}", certPath);
            LoadCertificate(certPath, certPassword);
            _logger?.LogInformation("Certificate successfully reloaded");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to reload certificate from: {CertPath}", certPath);
        }
    }

    private static void LoadCertificate(string certPath, string certPassword)
    {
        try
        {
            if (!File.Exists(certPath))
            {
                _logger?.LogWarning("Certificate file not found: {CertPath}", certPath);
                return;
            }

            var newCertificate = X509CertificateLoader.LoadPkcs12FromFile(certPath, certPassword);
            
            lock (_certificateLock)
            {
                // Dispose old certificate if it exists
                _currentCertificate?.Dispose();
                _currentCertificate = newCertificate;
            }

            _logger?.LogInformation("Certificate loaded successfully. Subject: {Subject}, Thumbprint: {Thumbprint}, Valid until: {ValidTo}", 
                newCertificate.Subject, newCertificate.Thumbprint, newCertificate.NotAfter);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading certificate from: {CertPath}", certPath);
            throw;
        }
    }
}