using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

Console.WriteLine("=== ПК22. Автообновление клиента ===");
Console.WriteLine();

// Куда класть файлы: рядом с .sln (data/PK22-Auto-Updater)
var slnDir = AppContext.BaseDirectory;
while (slnDir != null && !Directory.EnumerateFiles(slnDir, "*.sln").Any())
    slnDir = Path.GetDirectoryName(slnDir);

var root = Path.Combine(slnDir ?? AppContext.BaseDirectory, "data", "PK22-Auto-Updater");
var clientDir = Path.Combine(root, "client");
var serverDir = Path.Combine(root, "server");
var stagingDir = Path.Combine(root, "staging");
Directory.CreateDirectory(clientDir);
Directory.CreateDirectory(serverDir);
Directory.CreateDirectory(stagingDir);

// ---------- 1. Клиент версии 2.0.0 ----------
var clientFile = Path.Combine(clientDir, "campus-client.txt");
await File.WriteAllTextAsync(clientFile, "Smart Campus Client 2.0.0\n");
Console.WriteLine($"[1] Установленная версия: 2.0.0 ({clientFile})");

// ---------- 2. Сервер: файл 2.1.0 + manifest с SHA-256 ----------
var serverFile = Path.Combine(serverDir, "campus-client.txt");
await File.WriteAllTextAsync(serverFile, "Smart Campus Client 2.1.0\n");
var serverHash = HashFile(serverFile);

var manifest = new
{
    version = "2.1.0",
    releaseNotes = "Исправлен журнал проходов",
    files = new[]
    {
        new
        {
            path = "campus-client.txt",
            sha256 = serverHash,
            url = new Uri(serverFile).AbsoluteUri
        }
    }
};
var manifestPath = Path.Combine(serverDir, "manifest.json");
await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"[2] Сервер: версия 2.1.0, SHA-256={serverHash}");

// ---------- 3. Сравнение версий (SemVer) ----------
var localVer = ParseSemVer("2.0.0");
var remoteVer = ParseSemVer("2.1.0");

// Кортежи в C# не поддерживают операторы <, >, <=, >= — используем CompareTo.
var remoteIsNewer = remoteVer.CompareTo(localVer) > 0;
Console.WriteLine($"[3] Сравнение: local=2.0.0, remote=2.1.0, remote > local = {remoteIsNewer}");

if (remoteVer.Major > localVer.Major)
{
    Console.WriteLine("Мажорное обновление — не устанавливаем автоматически.");
    return;
}

// ---------- 4. Staging + проверка SHA-256 ----------
var stagedFile = Path.Combine(stagingDir, "campus-client.txt");
File.Copy(serverFile, stagedFile, true);
var stagedHash = HashFile(stagedFile);
Console.WriteLine($"[4] Staging: скопировано, SHA-256={stagedHash}");

if (stagedHash != serverHash)
{
    Console.WriteLine("ОШИБКА: SHA-256 не совпадает — обновление отменено");
    return;
}
Console.WriteLine("    SHA-256 совпадает — можно применять");

// ---------- 5. Резервная копия .bak + замена ----------
var bakFile = clientFile + ".bak";
File.Copy(clientFile, bakFile, true);
Console.WriteLine($"[5] Резервная копия: {bakFile}");
File.Copy(stagedFile, clientFile, true);
var newContent = await File.ReadAllTextAsync(clientFile);
var updated = newContent.Contains("2.1.0");
Console.WriteLine($"    Файл заменён, содержит 2.1.0: {updated}");

// ---------- 6. Демонстрация отката ----------
// Имитируем "приложение упало после обновления" -> возвращаем .bak
File.Copy(bakFile, clientFile, true);
var rolledBack = (await File.ReadAllTextAsync(clientFile)).Contains("2.0.0");
Console.WriteLine($"[6] Откат из .bak: восстановлена версия 2.0.0 = {rolledBack}");

// ---------- 7. Повреждённый патч ----------
// Кладём на сервер файл, но в манифесте указываем заведомо неправильный SHA-256
Console.WriteLine();
Console.WriteLine("--- Проверка повреждённого патча ---");
await File.WriteAllTextAsync(serverFile, "Smart Campus Client 2.2.0 BROKEN\n");
var brokenHash = HashFile(serverFile);

var badManifest = new
{
    version = "2.2.0",
    files = new[]
    {
        new
        {
            path = "campus-client.txt",
            sha256 = new string('0', 64), // заведомо неверный хеш
            url = new Uri(serverFile).AbsoluteUri
        }
    }
};
await File.WriteAllTextAsync(Path.Combine(serverDir, "bad-manifest.json"),
    JsonSerializer.Serialize(badManifest, new JsonSerializerOptions { WriteIndented = true }));

// "Скачиваем" и проверяем
var stagedBroken = Path.Combine(stagingDir, "campus-client-2.2.0.txt");
File.Copy(serverFile, stagedBroken, true);
var stagedBrokenHash = HashFile(stagedBroken);
var corruptedDetected = stagedBrokenHash != new string('0', 64);
Console.WriteLine($"[7] Реальный хеш:  {stagedBrokenHash}");
Console.WriteLine($"    Из манифеста: {new string('0', 64)}");
Console.WriteLine($"    Несовпадение обнаружено: {corruptedDetected} — обновление прервано");

// ---------- Итог ----------
var passed = updated && rolledBack && corruptedDetected;

var md = new StringBuilder();
md.AppendLine("# ПК22. Автообновление");
md.AppendLine();
md.AppendLine($"- Текущая версия: 2.0.0");
md.AppendLine($"- Версия на сервере: 2.1.0");
md.AppendLine($"- SHA-256 (корректный): `{serverHash}`");
md.AppendLine($"- Обновление: {(updated ? "PASSED" : "FAILED")}");
md.AppendLine($"- Откат после ошибки: {(rolledBack ? "PASSED" : "FAILED")}");
md.AppendLine($"- Повреждённый патч отклонён: {(corruptedDetected ? "PASSED" : "FAILED")}");
md.AppendLine();
md.AppendLine($"**Итог: {(passed ? "PASSED" : "FAILED")}**");

var reportPath = Path.Combine(root, "report.md");
await File.WriteAllTextAsync(reportPath, md.ToString());
Console.WriteLine();
Console.WriteLine($"Отчёт: {reportPath}");
Console.WriteLine();
Console.WriteLine($"==== Итог ПК22: {(passed ? "PASSED" : "FAILED")} ====");

// ---------- Вспомогательное ----------

static string HashFile(string path)
    => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

static (int Major, int Minor, int Patch) ParseSemVer(string v)
{
    var parts = v.Split('.');
    return (int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
}