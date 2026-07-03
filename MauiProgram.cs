using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using PdfSharpCore.Fonts;

namespace RunWise
{
    public static class MauiProgram
    {
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

            return builder.Build();
        }
    }
}
