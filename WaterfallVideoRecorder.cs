using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace KiwiDX;

internal sealed class WaterfallVideoRecorder : IAsyncDisposable
{
    public const int Width = 854;
    public const int Height = 480;
    public const int FramesPerSecond = 10;
    private readonly Process process;
    private readonly Stream input;
    private readonly string outputPath;
    private bool stopped;

    private WaterfallVideoRecorder(Process process, string outputPath)
    {
        this.process = process;
        input = process.StandardInput.BaseStream;
        this.outputPath = outputPath;
    }

    public static WaterfallVideoRecorder Start(string ffmpegPath)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"KiwiDX_{Guid.NewGuid():N}.mp4");
        var start = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "-y", "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", $"{Width}x{Height}",
            "-framerate", FramesPerSecond.ToString(), "-i", "-", "-an", "-c:v", "libx264",
            "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p", "-movflags", "+faststart", outputPath
        }) start.ArgumentList.Add(argument);
        var process = Process.Start(start) ?? throw new InvalidOperationException("FFmpeg could not be started.");
        process.ErrorDataReceived += (_, _) => { };
        process.BeginErrorReadLine();
        return new WaterfallVideoRecorder(process, outputPath);
    }

    public void Capture(Control control)
    {
        if (stopped || control.Width <= 0 || control.Height <= 0) return;
        using var source = new Bitmap(control.Width, control.Height, PixelFormat.Format32bppArgb);
        control.DrawToBitmap(source, new Rectangle(Point.Empty, source.Size));
        using var frame = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(frame))
        {
            graphics.Clear(Color.Black);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(source, new Rectangle(0, 0, Width, Height));
        }
        var data = frame.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[Width * Height * 4];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            input.Write(bytes, 0, bytes.Length);
        }
        finally { frame.UnlockBits(data); }
    }

    public async Task<string> StopAsync()
    {
        if (stopped) return outputPath;
        stopped = true;
        await input.FlushAsync();
        input.Dispose();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException($"FFmpeg ended with code {process.ExitCode}.");
        return outputPath;
    }

    public static async Task<string> AddAudioAsync(string ffmpegPath, string videoPath, string wavePath)
    {
        var combinedPath = Path.Combine(Path.GetTempPath(), $"KiwiDX_{Guid.NewGuid():N}_av.mp4");
        var start = new ProcessStartInfo
        {
            FileName = ffmpegPath, UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "-y", "-i", videoPath, "-i", wavePath, "-c:v", "copy", "-c:a", "aac", "-b:a", "128k",
            "-shortest", "-movflags", "+faststart", combinedPath
        }) start.ArgumentList.Add(argument);
        using var muxer = Process.Start(start) ?? throw new InvalidOperationException("FFmpeg audio muxer could not be started.");
        var errors = await muxer.StandardError.ReadToEndAsync();
        await muxer.WaitForExitAsync();
        if (muxer.ExitCode != 0) throw new InvalidOperationException($"FFmpeg could not add audio to the video. {errors.Trim()}");
        return combinedPath;
    }

    public async ValueTask DisposeAsync()
    {
        try { if (!stopped) await StopAsync(); } catch { if (!process.HasExited) process.Kill(true); }
        process.Dispose();
    }
}
