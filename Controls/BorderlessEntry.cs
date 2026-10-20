using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

namespace RunWise.Controls;

// Entry without the platform underline/border, meant to sit inside a styled Border.
public class BorderlessEntry : Entry
{
    static BorderlessEntry()
    {
        EntryHandler.Mapper.AppendToMapping("Borderless", (handler, view) =>
        {
            if (view is not BorderlessEntry) return;
#if ANDROID
            handler.PlatformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#elif WINDOWS
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
            handler.PlatformView.Background = null;
#endif
        });
    }
}
