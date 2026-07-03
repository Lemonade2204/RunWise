using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using RunWise.Models;
using RunWise.Services;
using SkiaSharp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RunWise.Pages;

[QueryProperty(nameof(RunId), "runId")]
public partial class RunDetailsPage : ContentPage
{
    private RunSession? _run;
    private readonly RunTrackerService _runService;

    public RunDetailsPage()
    {
        InitializeComponent();
        _runService = IPlatformApplication.Current?.Services.GetService<RunTrackerService>() ?? new RunTrackerService();
        BindingContext = this;
    }

    public string RunId
    {
        set
        {
            if (!string.IsNullOrEmpty(value))
                _ = LoadRunAsync(value);
        }
    }

    public DateTime StartTime => _run?.StartTime ?? DateTime.Now;
    public string DistanceFormatted => _run?.DistanceFormatted.ToString() ?? "0";
    public string DurationFormatted => _run?.DurationFormatted ?? "00:00:00";
    public string AveragePaceFormatted => _run?.AveragePaceFormatted ?? "0 km/h";

    private async Task LoadRunAsync(string id)
    {
        _run = await _runService.GetRunByIdAsync(id);
        OnPropertyChanged(nameof(StartTime));
        OnPropertyChanged(nameof(DistanceFormatted));
        OnPropertyChanged(nameof(DurationFormatted));
        OnPropertyChanged(nameof(AveragePaceFormatted));
    }

    private async void OnExportPdfClicked(object sender, EventArgs e)
    {
        //if (_run == null) return;

        //try
        //{
        //    var fileName = $"Run_{_run.StartTime:yyyyMMdd_HHmm}_{_run.Id}.pdf";
        //    var cachePath = Path.Combine(FileSystem.CacheDirectory, fileName);

        //    GenerateRunPdf(_run, cachePath);

        //    var request = new ShareFileRequest
        //    {
        //        Title = "Export Run",
        //        File = new ShareFile(cachePath)
        //    };

        //    await Share.RequestAsync(request);
        //}
        //catch (Exception ex)
        //{
        //    await DisplayAlert("Export failed", ex.Message, "OK");
        //}
        if (_run == null) return;

        try
        {
            var fileName = $"Run_{_run.StartTime:yyyyMMdd_HHmm}_{_run.Id}.pdf";
            var cachePath = Path.Combine(FileSystem.CacheDirectory, fileName);

            GenerateRunPdf(_run, cachePath);

            // Save a copy to the public Downloads folder
            var downloadsPath = Path.Combine(
                Android.OS.Environment.GetExternalStoragePublicDirectory(
                    Android.OS.Environment.DirectoryDownloads).AbsolutePath,
                fileName);

            File.Copy(cachePath, downloadsPath, true);

            // Also offer the share sheet, using the cache copy
            var request = new ShareFileRequest
            {
                Title = "Export Run",
                File = new ShareFile(cachePath)
            };

            await Share.RequestAsync(request);

            await DisplayAlert("Saved", $"PDF saved to Downloads:\n{downloadsPath}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Export failed", ex.Message, "OK");
        }
    }

    private void GenerateRunPdf(RunSession run, string path)
    {
        int width = 800, height = 400;

        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        // Fonts (size + typeface now live on SKFont, not SKPaint)
        using var titleTypeface = SKTypeface.FromFamilyName("sans-serif", SKFontStyle.Bold);
        using var titleFont = new SKFont(titleTypeface, 28);
        using var bodyFont = new SKFont(SKTypeface.FromFamilyName("sans-serif"), 18);
        using var footerFont = new SKFont(SKTypeface.FromFamilyName("sans-serif"), 14);

        // Paints now only hold color/style
        using var blackPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        using var grayPaint = new SKPaint { Color = SKColors.Gray, IsAntialias = true };

        canvas.DrawText("Run Report", 30, 50, titleFont, blackPaint);
        canvas.DrawText($"Date: {run.StartTime:MMM dd, yyyy - HH:mm}", 30, 100, bodyFont, blackPaint);
        canvas.DrawText($"Distance: {run.DistanceFormatted} km", 30, 130, bodyFont, blackPaint);
        canvas.DrawText($"Duration: {run.DurationFormatted}", 30, 160, bodyFont, blackPaint);
        canvas.DrawText($"Average Pace: {run.AveragePaceFormatted}", 30, 190, bodyFont, blackPaint);
        canvas.DrawText("(Exported from RunWise)", 30, 240, footerFont, grayPaint);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var imgStream = new MemoryStream(data.ToArray());

        var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(width);
        page.Height = XUnit.FromPoint(height);

        var gfx = XGraphics.FromPdfPage(page);
        var xImage = XImage.FromStream(() => imgStream);
        gfx.DrawImage(xImage, 0, 0, width, height);

        document.Save(path);
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}