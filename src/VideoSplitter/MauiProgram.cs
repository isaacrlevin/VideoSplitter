using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using VideoSplitter.Data;
using VideoSplitter.Models;
using VideoSplitter.Services;
using VideoSplitter.Services.SocialMediaPublishers;

namespace VideoSplitter
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            // Database
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "VideoSplitter");
            Directory.CreateDirectory(appFolder);
            var dbPath = Path.Combine(appFolder, "videosplitter.db");

            builder.Services.AddDbContext<VideoSplitterDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));

            // Services - SettingsService must be registered first since AppSettings depends on it
            builder.Services.AddSingleton<ISettingsService, SettingsService>();
            builder.Services.AddSingleton<IPromptService, PromptService>();

            // Register AppSettings as a singleton loaded from SettingsService at startup
            // Using Task.Run to avoid deadlock when calling async method synchronously on UI thread
            builder.Services.AddSingleton<AppSettings>(sp =>
            {
                var settingsService = sp.GetRequiredService<ISettingsService>();
                return Task.Run(async () => await settingsService.GetSettingsAsync()).GetAwaiter().GetResult();
            });

            // Services
            builder.Services.AddScoped<IProjectService, ProjectService>();
            builder.Services.AddScoped<ISegmentService, SegmentService>();
            builder.Services.AddScoped<IVideoService, VideoService>();
            builder.Services.AddSingleton<IFileStreamService, FileStreamService>();
            builder.Services.AddSingleton<IAudioExtractionService, AudioExtractionService>();
            builder.Services.AddSingleton<ITranscriptService, TranscriptService>();
            builder.Services.AddSingleton<ISubtitleService, SubtitleService>();
            builder.Services.AddScoped<IAiService, AiService>();
            builder.Services.AddScoped<IVideoExtractionService, VideoExtractionService>();
            
            // Social Media Publishing Services
            builder.Services.AddSingleton<ISocialMediaCredentialService, SocialMediaCredentialService>();
            builder.Services.AddScoped<ISocialMediaPublisherService, SocialMediaPublisherService>();
            
            // Platform-specific services
#if WINDOWS
            builder.Services.AddSingleton<IPlatformFileService, WindowsPlatformFileService>();
#else
            builder.Services.AddSingleton<IPlatformFileService, DefaultPlatformFileService>();
#endif

            // HTTP Client
            builder.Services.AddHttpClient();

            var app = builder.Build();

            // Initialize database
            using (var scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<VideoSplitterDbContext>();
                context.Database.EnsureCreated();
            }

            return app;
        }
    }
}
