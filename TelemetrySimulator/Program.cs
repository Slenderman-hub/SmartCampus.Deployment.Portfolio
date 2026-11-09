using System.Diagnostics;
using System.Text;

const string StationId = "ARM-KPP-01";
const string Endpoint = "https://httpbin.org/headers";

Console.WriteLine("=== ПК19. Симулятор телеметрии ===");
Console.WriteLine($"Станция: {StationId}");

var traceId = ActivityTraceId.CreateRandom().ToString();
var spanId = ActivitySpanId.CreateRandom().ToString();
var traceparent = $"00-{traceId}-{spanId}-01";

Console.WriteLine($"TraceId: {traceId}");
Console.WriteLine($"SpanId:  {spanId}");
Console.WriteLine($"traceparent: {traceparent}");

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
request.Headers.Add("traceparent", traceparent);
request.Headers.Add("X-Mock-Trace-Id", traceId);

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
    passed = response.IsSuccessStatusCode && body.Contains(traceparent) && body.Contains(traceId);
}
catch (Exception ex)
{
    error = ex.Message;
}

Console.WriteLine($"HTTP: {statusCode}, время: {ms} мс");
if (error != null) Console.WriteLine($"Ошибка: {error}");

// Сохраняем отчёт в папку data рядом с решением
var baseDir = AppContext.BaseDirectory;
var slnDir = baseDir;
while (slnDir != null && !File.Exists(Path.Combine(slnDir, "SmartCampus.Deployment.Portfolio.sln")))
    slnDir = Path.GetDirectoryName(slnDir);

var dataDir = Path.Combine(slnDir ?? baseDir, "data", "PK19-Telemetry");
Directory.CreateDirectory(dataDir);

var md = new StringBuilder();
md.AppendLine("# ПК19. Телеметрия и трассировка");
md.AppendLine();
md.AppendLine($"- Станция: `{StationId}`");
md.AppendLine($"- TraceId: `{traceId}`");
md.AppendLine($"- SpanId: `{spanId}`");
md.AppendLine($"- traceparent: `{traceparent}`");
md.AppendLine($"- HTTP: {statusCode}");
md.AppendLine($"- Время: {ms} мс");
if (error != null) md.AppendLine($"- Ошибка: `{error}`");
md.AppendLine();
md.AppendLine($"**Итог: {(passed ? "PASSED" : "FAILED")}**");

var reportPath = Path.Combine(dataDir, "telemetry_report.md");
await File.WriteAllTextAsync(reportPath, md.ToString());
Console.WriteLine($"Отчёт: {reportPath}");

Console.WriteLine();
Console.WriteLine($"==== Итог ПК19: {(passed ? "PASSED" : "FAILED")} ====");