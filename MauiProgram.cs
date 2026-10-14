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

            // Supabase configuration (replace keys if needed)
            var supabaseUrl = "https://msfowrhtwzlltjszjcqg.supabase.co";
            var supabaseAnonKey = "sb_publishable_YITu2bU0pknKgfBqG1Y4mw_LyPf8Jdp";

            // Register Supabase services
            builder.Services.AddSingleton(sp => new SupabaseAuthService(supabaseUrl, supabaseAnonKey));
            builder.Services.AddSingleton(sp => new SupabaseDatabaseService(supabaseUrl, supabaseAnonKey, sp.GetRequiredService<SupabaseAuthService>()));
            // Meal planner store
            builder.Services.AddSingleton<RunWise.Services.MealStore>();

            // Register Calorie API client (used by FoodRecommendationPage)
            builder.Services.AddHttpClient<CalorieApiService>(client =>
            {
                client.BaseAddress = new Uri("https://calorieapiadmin.com/");
            });

            // MuscleWiki API removed - use local routines instead

            // OpenRouterService and RunTracker
            builder.Services.AddSingleton<OpenRouterService>();
            builder.Services.AddSingleton<RunTrackerService>();

            // Register ViewModels
            builder.Services.AddTransient<RunWise.ViewModels.ExercisesViewModel>();

            // Enable IHttpClientFactory for any other consumers
            builder.Services.AddHttpClient();

            var app = builder.Build();
            Services = app.Services;
            return app;
        }
    }
}
