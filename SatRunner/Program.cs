using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

var stationId = Environment.GetEnvironmentVariable("CAMPUS_WORKSTATIONID") ?? "ARM-KPP-01";
var environment_ = Environment.GetEnvironmentVariable("CAMPUS_ENVIRONMENT") ?? "Staging";
var apiUrl = Environment.GetEnvironmentVariable("CAMPUS_APIBASEURL") ?? "https://httpbin.org/headers";
var security = Environment.GetEnvironmentVariable("CAMPUS_CAMPUSSECURITY") ?? "demo-token-2026";

Console.WriteLine("=== ПК21. Приёмочные испытания (SAT) ===");
Console.WriteLine($"АРМ: {stationId}");
Console.WriteLine($"Окружение: {environment_}");
Console.WriteLine($"API: {apiUrl}");
Console.WriteLine();

var checks = new List<(string Id, string Name, bool Ok, string Details)>();

var idPattern = new Regex(@"^ARM-[A-Z]{3}-\d{2}$");
var sat01Ok = !string.IsNullOrWhiteSpace(stationId) && idPattern.IsMatch(stationId);
checks.Add(("SAT-01", "Корректность идентификатора АРМ", sat01Ok,
    $"'{stationId}' {(sat01Ok ? "соответствует" : "не соответствует")} шаблону ARM-XXX-NN"));

int statusCode = 0;
long latencyMs = 0;
string body = "";
string error = null;

try
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
    request.Headers.Add("X-Station-Id", stationId);
    request.Headers.Add("X-Campus-Security", security);

    var sw = Stopwatch.StartNew();
    using var response = await http.SendAsync(request);
    sw.Stop();
    latencyMs = sw.ElapsedMilliseconds;
    statusCode = (int)response.StatusCode;
    body = await response.Content.ReadAsStringAsync();
}
catch (Exception ex)
{
    error = $"{ex.GetType().Name}: {ex.Message}";
}

var sat02Ok = statusCode == 200 && latencyMs < 3000;
checks.Add(("SAT-02", "Доступность внешнего API", sat02Ok,
    error ?? $"HTTP {statusCode}, {latencyMs} мс (порог 3000 мс)"));

var sat03Ok = !string.IsNullOrEmpty(body)
              && body.Contains(stationId)
              && body.Contains(security);
checks.Add(("SAT-03", "Передача служебных заголовков", sat03Ok,
    $"X-Station-Id={stationId}, X-Campus-Security {(sat03Ok ? "передан" : "не передан")}"));

var allowed = new[] { "Staging", "Production" };
var sat04Ok = allowed.Contains(environment_);
checks.Add(("SAT-04", "Окружение развёртывания", sat04Ok,
    $"{environment_} {(sat04Ok ? "входит" : "не входит")} в список Staging/Production"));

foreach (var c in checks)
    Console.WriteLine($"[{(c.Ok ? "PASSED" : "FAILED")}] {c.Id} {c.Name} — {c.Details}");

var passedCount = checks.Count(c => c.Ok);
var allOk = passedCount == checks.Count;

// Сохранение: ищем .sln, чтобы положить отчёт в <решение>/data/...
var slnDir = AppContext.BaseDirectory;
while (slnDir != null && !Directory.EnumerateFiles(slnDir, "*.sln").Any())
    slnDir = Path.GetDirectoryName(slnDir);

var dataDir = Path.Combine(slnDir ?? AppContext.BaseDirectory, "data", "PK21-Acceptance-Testing");
Directory.CreateDirectory(dataDir);

var md = new StringBuilder();
md.AppendLine("# ПК21. SAT Protocol Report");
md.AppendLine();
md.AppendLine($"- АРМ: `{stationId}`");
md.AppendLine($"- Окружение: `{environment_}`");
md.AppendLine($"- API: `{apiUrl}`");
md.AppendLine();
md.AppendLine("| ID | Проверка | Результат | Детали |");
md.AppendLine("|---|---|---|---|");
foreach (var c in checks)
    md.AppendLine($"| {c.Id} | {c.Name} | {(c.Ok ? "PASSED" : "FAILED")} | {c.Details} |");
md.AppendLine();
md.AppendLine($"**Итог: {(allOk ? "PASSED" : "FAILED")} ({passedCount}/{checks.Count})**");

var reportPath = Path.Combine(dataDir, "SAT_Protocol_Report.md");
await File.WriteAllTextAsync(reportPath, md.ToString());
Console.WriteLine();
Console.WriteLine($"Протокол: {reportPath}");

Console.WriteLine();
Console.WriteLine($"==== Итог ПК21: {(allOk ? "PASSED" : "FAILED")} ({passedCount}/{checks.Count}) ====");