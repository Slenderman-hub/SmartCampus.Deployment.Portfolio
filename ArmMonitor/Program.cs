using System.Diagnostics;
using System.Text;
using Serilog;

const string StationId = "ARM-KPP-DESKTOP-QU5525";
const string Environment_ = "Test";
const string Endpoint = "https://httpbin.org/headers";

Console.WriteLine("=== ПК20. Мониторинг АРМ ===");
Console.WriteLine($"Станция: {StationId}");
Console.WriteLine($"Окружение: {Environment_}");
Console.WriteLine();

// Папка для данных: ищем корень решения по .sln
var slnDir = AppContext.BaseDirectory;
while (slnDir != null && !Directory.EnumerateFiles(slnDir, "*.sln").Any())
    slnDir = Path.GetDirectoryName(slnDir);

var dataRoot = Path.Combine(slnDir ?? AppContext.BaseDirectory, "data", "PK20-Monitoring");
var logDir = Path.Combine(dataRoot, "logs");
Directory.CreateDirectory(logDir);

// Настраиваем Serilog: консоль + файл с ротацией по дням
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}")
    .WriteTo.File(
        Path.Combine(logDir, "monitor-.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}")
    .CreateLogger();

Log.Information("Мониторинг запущен. Станция={Station}, Env={Env}", StationId, Environment_);

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
request.Headers.Add("X-Station-Id", StationId);
request.Headers.Add("X-Environment", Environment_);

var passed = false;
var statusCode = 0;
long ms = 0;
var body = "";
string error = null;

try
{
    var sw = Stopwatch.StartNew();
    using var response = await http.SendAsync(request);
    sw.Stop();
    ms = sw.ElapsedMilliseconds;
    statusCode = (int)response.StatusCode;
    body = await response.Content.ReadAsStringAsync();

    passed = response.IsSuccessStatusCode
             && body.Contains(StationId)
             && body.Contains(Environment_);

    Log.Information("HTTP={Status}, latency={Latency}ms, station={Station}",
        statusCode, ms, StationId);
}
catch (Exception ex)
{
    error = $"{ex.GetType().Name}: {ex.Message}";
    Log.Error("Ошибка мониторинга: {Error}", error);
}

await Log.CloseAndFlushAsync();

Console.WriteLine($"HTTP: {statusCode}, время: {ms} мс");
if (error != null) Console.WriteLine($"Ошибка: {error}");

var md = new StringBuilder();
md.AppendLine("# ПК20. Мониторинг функционирования");
md.AppendLine();
md.AppendLine($"- Рабочая станция: `{StationId}`");
md.AppendLine($"- Окружение: `{Environment_}`");
md.AppendLine($"- Endpoint: `{Endpoint}`");
md.AppendLine($"- HTTP: {statusCode}");
md.AppendLine($"- Время ответа: {ms} мс");
if (error != null) md.AppendLine($"- Ошибка: `{error}`");
md.AppendLine();
md.AppendLine($"**Итог: {(passed ? "PASSED" : "FAILED")}**");

var reportPath = Path.Combine(dataRoot, "report.md");
await File.WriteAllTextAsync(reportPath, md.ToString());
Console.WriteLine($"Отчёт: {reportPath}");

Console.WriteLine();
Console.WriteLine($"==== Итог ПК20: {(passed ? "PASSED" : "FAILED")} ====");