using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

Console.WriteLine("=== ПК24. Контур информационной безопасности ===");
Console.WriteLine();

// Корень data/ рядом с .sln
var slnDir = AppContext.BaseDirectory;
while (slnDir != null && !Directory.EnumerateFiles(slnDir, "*.sln").Any())
    slnDir = Path.GetDirectoryName(slnDir);

var root = Path.Combine(slnDir ?? AppContext.BaseDirectory, "data", "PK24-Security");
Directory.CreateDirectory(root);

var auditPath = Path.Combine(root, "security-audit.log");
if (File.Exists(auditPath)) File.Delete(auditPath);

void Audit(string level, string message)
{
    var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
    File.AppendAllText(auditPath, line + Environment.NewLine);
    Console.WriteLine($"    [{level}] {message}");
}

Audit("INFO", "Контур безопасности запущен");

// ---------- 1. DPAPI — шифрование секрета ключом Windows-аккаунта ----------
Console.WriteLine("[1] DPAPI — шифрование секрета:");
var secret = "campus-api-key-2026";
var encrypted = ProtectedData.Protect(
    Encoding.UTF8.GetBytes(secret),
    optionalEntropy: null,
    scope: DataProtectionScope.CurrentUser);
var secretsPath = Path.Combine(root, "secrets.dat");
File.WriteAllBytes(secretsPath, encrypted);

var decrypted = ProtectedData.Unprotect(
    File.ReadAllBytes(secretsPath),
    optionalEntropy: null,
    scope: DataProtectionScope.CurrentUser);
var dpapiOk = Encoding.UTF8.GetString(decrypted) == secret;
Audit("INFO", $"DPAPI: секрет зашифрован, файл {secretsPath} ({encrypted.Length} байт)");
Audit("INFO", $"DPAPI: расшифровка прошла = {dpapiOk}");
Console.WriteLine();

// ---------- 2. BCrypt — пароль оператора ----------
Console.WriteLine("[2] BCrypt — хеширование пароля оператора:");
const string demoPassword = "Operator!2026";
var hash = BCrypt.Net.BCrypt.HashPassword(demoPassword);
Audit("INFO", $"BCrypt: хеш пароля создан (длина {hash.Length})");

var rightPass = BCrypt.Net.BCrypt.Verify(demoPassword, hash);
Audit(rightPass ? "INFO" : "ERROR", $"BCrypt: правильный пароль принят = {rightPass}");

var wrongPass = !BCrypt.Net.BCrypt.Verify("wrong-password", hash);
Audit(wrongPass ? "INFO" : "ERROR", $"BCrypt: неверный пароль отклонён = {wrongPass}");

// Имитация блокировки после 3 неудачных попыток
var failedAttempts = 0;
for (var i = 0; i < 3; i++)
    if (!BCrypt.Net.BCrypt.Verify("bad", hash)) failedAttempts++;
var locked = failedAttempts >= 3;
if (locked) Audit("WARN", $"BCrypt: после {failedAttempts} неверных попыток учётная запись заблокирована");
Console.WriteLine();

// ---------- 3. X.509 — проверка цепочки доверия ----------
Console.WriteLine("[3] X.509 — проверка сертификатов:");

// 3.1 Доверенный: сертификат github.com
var (githubTrusted, githubDetails) = await CheckRemoteCertAsync("github.com");
Audit(githubTrusted ? "INFO" : "WARN",
    $"X.509: github.com сертификат {(githubTrusted ? "доверенный" : "недоверенный")} ({githubDetails})");

// 3.2 Самоподписанный: генерируем в памяти
using var rsa = RSA.Create(2048);
var certReq = new CertificateRequest("CN=self-signed-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using var selfSigned = certReq.CreateSelfSigned(
    DateTimeOffset.Now.AddDays(-1),
    DateTimeOffset.Now.AddDays(1));

using var chain = new X509Chain();
chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
var selfSignedTrusted = chain.Build(selfSigned);
var status = chain.ChainStatus.Length > 0 ? chain.ChainStatus[0].Status.ToString() : "OK";
Audit("WARN", $"X.509: самоподписанный сертификат {(selfSignedTrusted ? "принят" : "отклонён")} (статус: {status})");
Console.WriteLine();

// ---------- 4. RBAC — ролевое разграничение ----------
Console.WriteLine("[4] RBAC — роли и права:");
var permissions = new Dictionary<string, HashSet<string>>
{
    ["Operator"] = new() { "OpenBarrier" },
    ["Supervisor"] = new() { "OpenBarrier", "ViewJournal", "ExportReport" },
    ["Administrator"] = new() { "OpenBarrier", "ViewJournal", "ExportReport", "ManageUsers" }
};

bool HasAccess(string role, string action)
    => permissions.TryGetValue(role, out var perms) && perms.Contains(action);

var rbacTests = new (string Role, string Action, bool Expected)[]
{
    ("Operator",      "OpenBarrier",  true),
    ("Operator",      "ViewJournal",  false),
    ("Supervisor",    "ViewJournal",  true),
    ("Supervisor",    "ManageUsers",  false),
    ("Administrator", "ManageUsers",  true),
};

var rbacOk = true;
foreach (var t in rbacTests)
{
    var has = HasAccess(t.Role, t.Action);
    if (has != t.Expected) rbacOk = false;
    Audit(has ? "INFO" : "WARN",
        $"RBAC: роль={t.Role} действие={t.Action} {(has ? "разрешено" : "запрещено")}");
}
Console.WriteLine();

// ---------- Итог ----------
var passed = dpapiOk && rightPass && wrongPass && locked && githubTrusted && !selfSignedTrusted && rbacOk;

var md = new StringBuilder();
md.AppendLine("# ПК24. Информационная безопасность");
md.AppendLine();
md.AppendLine("## Результаты проверок");
md.AppendLine();
md.AppendLine("| Механизм | Проверка | Статус |");
md.AppendLine("|---|---|---|");
md.AppendLine($"| DPAPI | Секрет зашифрован и расшифрован | {(dpapiOk ? "PASSED" : "FAILED")} |");
md.AppendLine($"| BCrypt | Правильный пароль принят | {(rightPass ? "PASSED" : "FAILED")} |");
md.AppendLine($"| BCrypt | Неверный пароль отклонён | {(wrongPass ? "PASSED" : "FAILED")} |");
md.AppendLine($"| BCrypt | Блокировка после 3 ошибок | {(locked ? "PASSED" : "FAILED")} |");
md.AppendLine($"| X.509 | Доверенный сертификат принят | {(githubTrusted ? "PASSED" : "FAILED")} |");
md.AppendLine($"| X.509 | Самоподписанный отклонён | {(!selfSignedTrusted ? "PASSED" : "FAILED")} |");
md.AppendLine($"| RBAC | Разграничение доступа | {(rbacOk ? "PASSED" : "FAILED")} |");
md.AppendLine();
md.AppendLine($"**Итог: {(passed ? "PASSED" : "FAILED")}**");
md.AppendLine();
md.AppendLine($"Журнал аудита: `{auditPath}`");

var reportPath = Path.Combine(root, "report.md");
await File.WriteAllTextAsync(reportPath, md.ToString());

Audit("INFO", $"Контур безопасности завершён, итог {(passed ? "PASSED" : "FAILED")}");

Console.WriteLine($"Отчёт: {reportPath}");
Console.WriteLine($"Журнал: {auditPath}");
Console.WriteLine();
Console.WriteLine($"==== Итог ПК24: {(passed ? "PASSED" : "FAILED")} ====");

// ---------- Вспомогательное ----------

static async Task<(bool Trusted, string Details)> CheckRemoteCertAsync(string host)
{
    try
    {
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(host, 443);
        using var ssl = new SslStream(tcp.GetStream(), false, (_, _, _, _) => true);
        await ssl.AuthenticateAsClientAsync(host);

        using var cert = new X509Certificate2(ssl.RemoteCertificate);
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        var trusted = chain.Build(cert);
        var details = trusted
            ? $"subject={cert.Subject}, expires={cert.NotAfter:yyyy-MM-dd}"
            : string.Join("; ", chain.ChainStatus.Select(s => s.Status.ToString()));
        return (trusted, details);
    }
    catch (Exception ex)
    {
        return (false, ex.Message);
    }
}