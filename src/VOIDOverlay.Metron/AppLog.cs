using System.Diagnostics;
using System.IO;

namespace VOIDOverlay.Metron;

public static class AppLog
{
    private static readonly object Gate = new();
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VOIDOverlay",
        "Metron",
        "logs");

    public static void Warning(string message) => Write("WARN", message);

    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:O} [{level}] {message}";
        Trace.WriteLine(line);
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                File.AppendAllText(Path.Combine(DirectoryPath, $"metron-{DateTime.Now:yyyyMMdd}.log"), line + Environment.NewLine);
            }
        }
        catch (IOException ex)
        {
            Trace.WriteLine($"Unable to write Metron log: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Trace.WriteLine($"Unable to write Metron log: {ex.Message}");
        }
    }
}
