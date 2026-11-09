# SmartCampus Deployment Portfolio

Портфолио по МДК.03.01 «Внедрение и поддержка компьютерных систем».
Практическая работа №8 — объединение модулей ПК19–ПК25 в единый проект.

## Состав

| Модуль | Проект | Назначение |
|---|---|---|
| ПК19 | TelemetrySimulator | Телеметрия и W3C Trace Context |
| ПК20 | ArmMonitor | Мониторинг АРМ, Serilog, ротация логов |
| ПК21 | SatRunner | Приёмочные SAT-испытания, протокол Markdown |
| ПК22 | AutoUpdater | SemVer, SHA-256, staging, `.bak`, откат |
| ПК23 | OfflineBuffer | SQLite + EF Core + Polly, очередь событий |
| ПК24 | SecurityContour | DPAPI, BCrypt, X.509, RBAC, аудит |
| ПК25 | BackupAgent + scripts | ZIP, SHA-256, Bearer, restore с проверкой |

## Требования

- .NET 8 SDK
- Windows (DPAPI работает только на Windows)
- PowerShell 5.1+

## Запуск модуля

Каждый проект — консольное приложение:

```powershell
dotnet run --project TelemetrySimulator
dotnet run --project ArmMonitor
dotnet run --project SatRunner
dotnet run --project AutoUpdater
dotnet run --project OfflineBuffer
dotnet run --project SecurityContour
dotnet run --project BackupAgent
