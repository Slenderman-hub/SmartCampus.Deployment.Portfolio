using System.Text;
using Microsoft.EntityFrameworkCore;
using Polly;

Console.WriteLine("=== ПК23. Offline-First: локальная очередь событий ===");
Console.WriteLine();

// Путь к data/ рядом с .sln
var slnDir = AppContext.BaseDirectory;
while (slnDir != null && !Directory.EnumerateFiles(slnDir, "*.sln").Any())
    slnDir = Path.GetDirectoryName(slnDir);

var root = Path.Combine(slnDir ?? AppContext.BaseDirectory, "data", "PK23-Offline-First");
Directory.CreateDirectory(root);

var dbPath = Path.Combine(root, "attendance.db");
if (File.Exists(dbPath)) File.Delete(dbPath); // чистый старт

var options = new DbContextOptionsBuilder<CampusDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;

// 1. Создаём БД и заполняем кэш студентов
await using (var db = new CampusDbContext(options))
{
    await db.Database.EnsureCreatedAsync();
    db.Students.AddRange(
        new CachedStudent { CardNumber = "CARD-0001", FullName = "Иванов Пётр (И14-23-9)" },
        new CachedStudent { CardNumber = "CARD-0002", FullName = "Смирнова Анна (И14-23-9)" },
        new CachedStudent { CardNumber = "CARD-0003", FullName = "Кузнецов Олег (И14-23-9)" }
    );
    await db.SaveChangesAsync();
}

Console.WriteLine($"[1] SQLite: {dbPath}");
Console.WriteLine($"[2] Кэш студентов загружен: 3 записи");
Console.WriteLine();

// 3. Регистрация посещений (работает БЕЗ сети — события просто пишутся в БД)
Console.WriteLine("[3] Регистрация посещений (работает БЕЗ сети):");
var cardNumbers = new[] { "CARD-0001", "CARD-0002", "CARD-0003", "CARD-9999" };
await using (var db = new CampusDbContext(options))
{
    foreach (var card in cardNumbers)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.CardNumber == card);
        if (student == null)
        {
            Console.WriteLine($"    {card}: карта не найдена в локальном кэше — пропуск");
            continue;
        }
        db.Attendance.Add(new OfflineEventAttendance
        {
            CardNumber = card,
            Timestamp = DateTime.Now,
            IsSynced = false
        });
        Console.WriteLine($"    {card}: {student.FullName} — сохранено локально, IsSynced = false");
    }
    await db.SaveChangesAsync();
}

int queueBefore;
await using (var db = new CampusDbContext(options))
    queueBefore = await db.Attendance.CountAsync(a => !a.IsSynced);

Console.WriteLine($"    В очереди на синхронизацию: {queueBefore}");
Console.WriteLine();

// 4. Polly: 3 повтора, экспоненциальная задержка 1-2-4 с
var retry = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)),
        onRetry: (exception, delay, attempt, _) =>
            Console.WriteLine($"      Polly: попытка {attempt} не удалась ({exception.Message}), повтор через {delay.TotalSeconds:0}с"));

// Имитация сервера — пока недоступен
var serverUp = false;

async Task PushEvent(OfflineEventAttendance evt)
{
    if (!serverUp)
        throw new HttpRequestException("Сервер недоступен");
    await Task.Delay(50); // "POST /attendance"
}

// 5. Фаза 1 — сервер лежит. Пробуем отправить ОДНО событие, чтобы показать retry.
Console.WriteLine("[4] Фаза 1 — сервер недоступен (демо retry на одном событии):");
await using (var db = new CampusDbContext(options))
{
    var first = await db.Attendance.FirstOrDefaultAsync(a => !a.IsSynced);
    if (first != null)
    {
        try
        {
            await retry.ExecuteAsync(async () => await PushEvent(first));
            first.IsSynced = true;
            Console.WriteLine($"    {first.CardNumber}: синхронизировано");
        }
        catch (HttpRequestException)
        {
            Console.WriteLine($"    {first.CardNumber}: после 3 повторов не удалось — остаётся в очереди");
        }
        await db.SaveChangesAsync();
    }
}

// 6. Фаза 2 — сервер снова доступен, синхронизируем очередь
Console.WriteLine();
Console.WriteLine("[5] Фаза 2 — связь восстановлена, синхронизация очереди:");
serverUp = true;

await using (var db = new CampusDbContext(options))
{
    var pending = await db.Attendance.Where(a => !a.IsSynced).ToListAsync();
    foreach (var evt in pending)
    {
        await retry.ExecuteAsync(async () => await PushEvent(evt));
        evt.IsSynced = true;
        Console.WriteLine($"    {evt.CardNumber}: синхронизировано");
    }
    await db.SaveChangesAsync();
}

int queueAfter;
await using (var db = new CampusDbContext(options))
    queueAfter = await db.Attendance.CountAsync(a => !a.IsSynced);

Console.WriteLine();
Console.WriteLine($"[6] Очередь до синхронизации: {queueBefore}");
Console.WriteLine($"    Очередь после синхронизации: {queueAfter}");

var passed = queueBefore == 3 && queueAfter == 0;

// 7. Отчёт
var md = new StringBuilder();
md.AppendLine("# ПК23. Offline-First");
md.AppendLine();
md.AppendLine($"- SQLite: `{dbPath}`");
md.AppendLine($"- Событий в очереди до синхронизации: {queueBefore}");
md.AppendLine($"- Событий после синхронизации: {queueAfter}");
md.AppendLine($"- Политика Polly: 3 повтора, экспоненциальная задержка 1-2-4 с");
md.AppendLine();
md.AppendLine($"**Итог: {(passed ? "PASSED" : "FAILED")}**");

var reportPath = Path.Combine(root, "report.md");
await File.WriteAllTextAsync(reportPath, md.ToString());
Console.WriteLine();
Console.WriteLine($"Отчёт: {reportPath}");
Console.WriteLine();
Console.WriteLine($"==== Итог ПК23: {(passed ? "PASSED" : "FAILED")} ====");

// ---------- Сущности и DbContext ----------

public class CachedStudent
{
    public int Id { get; set; }
    public string CardNumber { get; set; } = "";
    public string FullName { get; set; } = "";
}

public class OfflineEventAttendance
{
    public int Id { get; set; }
    public string CardNumber { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public bool IsSynced { get; set; }
}

public class CampusDbContext : DbContext
{
    public DbSet<CachedStudent> Students => Set<CachedStudent>();
    public DbSet<OfflineEventAttendance> Attendance => Set<OfflineEventAttendance>();

    public CampusDbContext(DbContextOptions<CampusDbContext> options) : base(options) { }
}