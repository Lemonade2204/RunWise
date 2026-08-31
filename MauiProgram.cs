using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using PdfSharpCore.Fonts;
using RunWise.Services;


namespace RunWise
{
    public static class MauiProgram
    {
        public static IServiceProvider Services { get; private set; } = default!;
        public static MauiApp CreateMauiApp()
        {
            GlobalFontSettings.FontResolver = new DummyFontResolver();
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            // Register SupabaseAuthService. Replace the placeholders with your Supabase URL and anon key.
            var supabaseUrl = "https://msfowrhtwzlltjszjcqg.supabase.co";
            var supabaseAnonKey = "sb_publishable_YITu2bU0pknKgfBqG1Y4mw_LyPf8Jdp"; // your anon/public key
            // Register Supabase auth instance
            var supabaseAuth = new SupabaseAuthService(supabaseUrl, supabaseAnonKey);
            builder.Services.AddSingleton(supabaseAuth);

            // Register Supabase database service and pass the auth instance so it can use the user's token
            builder.Services.AddSingleton(new SupabaseDatabaseService(supabaseUrl, supabaseAnonKey, supabaseAuth));

            // Register simple food recommendation service
            builder.Services.AddSingleton<FoodRecommendationService>();
            // Register Food API client
            builder.Services.AddHttpClient<FoodApiService>();



            var app = builder.Build();
            Services = app.Services;
            return app;
        }
    }
}
