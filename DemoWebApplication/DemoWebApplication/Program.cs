using DemoWebApplication.Extensions;
using DemoWebApplication.Services;
using static DemoWebApplication.Extensions.CertificateServiceExtensions;

var builder = WebApplication.CreateBuilder(args);

// Add certificate services for OpenShift TLS support
builder.Services.AddCertificateServices(builder.Configuration);

// Configure Kestrel with custom certificate selector
builder.ConfigureKestrelWithCertificateSelector();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Start file watcher after app is built and services are available
var fileWatcherService = app.Services.GetService<IFileWatcherService>();
if (fileWatcherService != null)
{
    var certPath = app.Configuration["Certificate:Path"] ?? "/app/certs/server.pfx";
    var certPassword = app.Configuration["Certificate:Password"] ?? "";
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    
    // Set up certificate reload callback
    fileWatcherService.SetCertificateChangedCallback((changedFilePath) =>
    {
        try
        {
            logger.LogInformation("Certificate file changed: {ChangedFilePath}, reloading Kestrel certificate", changedFilePath);
            ReloadCertificate(changedFilePath, certPassword);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reload certificate after file change: {ChangedFilePath}", changedFilePath);
        }
    });
    
    _ = Task.Run(async () =>
    {
        try
        {
            logger.LogInformation("Starting certificate file watcher for path: {CertPath}", certPath);
            await fileWatcherService.StartWatchingAsync(certPath, "*.pfx");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start certificate file watcher");
        }
    });
}

app.Run();