using System.Text;

namespace SmartCampus.Shared;

public static class ReportWriter
{
    public static async Task<string> SaveAsync(string moduleFolder, string fileName, string markdown)
    {
        var root = FindRepoRoot();
        var dir = Path.Combine(root, "data", moduleFolder);
        Directory.CreateDirectory(dir);

        var full = Path.Combine(dir, fileName);
        await File.WriteAllTextAsync(full, markdown, Encoding.UTF8);
        Console.WriteLine($"Отчёт сохранён: {full}");
        return full;
    }

    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SmartCampus.Deployment.Portfolio.sln")))
            dir = dir.Parent;

        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    public static void PrintResult(string module, bool passed)
    {
        Console.WriteLine();
        Console.WriteLine($"==== Итог {module}: {(passed ? "PASSED" : "FAILED")} ====");
        if (!passed) Environment.ExitCode = 1;
    }
}