//using PdfSharpCore.Fonts;

//public class CustomFontResolver : IFontResolver
//{
//    public byte[] GetFont(string faceName)
//    {
//        using var stream = FileSystem.OpenAppPackageFileAsync("Roboto-Regular.ttf").GetAwaiter().GetResult();
//        using var ms = new MemoryStream();
//        stream.CopyTo(ms);
//        return ms.ToArray();
//    }

//    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
//    {
//        return new FontResolverInfo("Roboto#Regular"); // always same font, ignore bold/italic requests
//    }
//}

using PdfSharpCore.Fonts;

public class DummyFontResolver : IFontResolver
{
    public string DefaultFontName => "Roboto";

    public byte[] GetFont(string faceName) => Array.Empty<byte>();

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => new FontResolverInfo(familyName);
}