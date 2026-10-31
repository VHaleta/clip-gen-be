# Техническая спецификация — TikTok Clip Generator

## 1. Архитектура

```
┌──────────────────────────── Локальная машина ─────────────────────────────┐
│                                                                           │
│  Браузер                                                                  │
│   ├── React SPA  (/)        ─┐                                            │
│   └── Razor Admin (/admin)  ─┼──► ClipGen.Api  (ASP.NET Core 10)          │
│                              │      ├── Controllers (REST API)            │
│                              │      ├── ClipGen.Admin (Razor, App Part)   │
│                              │      ├── Hangfire Server (очередь, 1 воркер)│
│                              │      ├── Whisper.net (CUDA)                │
│                              │      ├── FFmpeg / FFprobe                  │
│                              │      └── Файловое хранилище (видео, кэш)   │
└──────────────────────────────┴───────┬─────────────────┬────────────┬─────┘
                                       │                 │            │
                            Azure SQL Database    Azure Key Vault   Внешние AI API
                            ├ данные приложения   ├ API-ключи AI    ├ Google Gemini
                            ├ ASP.NET Identity    ├ строка подкл.   ├ Anthropic Claude
                            ├ Hangfire            └ ключ защиты     └ ElevenLabs
                            └ ключи Data Protection  Data Protection
```

- В production SPA собирается в `ClipGen.Api/wwwroot` и отдаётся с того же origin, что и API.
- В разработке SPA работает на Vite (порт 3000) и проксирует `/api` на `https://localhost:5001` (раздел 19.4). Для браузера это один origin, поэтому CORS не используется, а cookie `SameSite=Strict` работают без исключений. Админка в разработке открывается напрямую на `https://localhost:5001/admin`.
- Прогресс обработки передаётся через Server-Sent Events.

---

## 2. Структура решения

```
ClipGen.sln
├── src/
│   ├── ClipGen.Core/                   // домен и абстракции, без зависимостей от инфраструктуры
│   │   ├── Domain/                     // сущности, record-типы, enum
│   │   ├── Abstractions/               // ILlmProvider, ITtsProvider, ITranscriptionService, IVideoRenderer, ...
│   │   ├── Pipeline/                   // шаги, расчёт прогресса, правила длины клипа и нарратива
│   │   └── Text/                       // регулярные выражения (GeneratedRegex), разбивка на предложения
│   │
│   ├── ClipGen.Infrastructure/
│   │   ├── Data/                       // AppDbContext, конфигурации Fluent API, миграции, Dapper-запросы
│   │   ├── Data/HangfireRead/          // scaffold-контекст схемы HangFire (только чтение)
│   │   ├── Ai/Gemini/                  // Flurl
│   │   ├── Ai/Anthropic/               // RestSharp
│   │   ├── Ai/ElevenLabs/              // Refit
│   │   ├── Media/                      // FFMpegCore, FFprobe, построитель графа фильтров, прокси-превью, Whisper.net
│   │   ├── Caching/                    // кэширующие декораторы, учёт ссылок на файлы кэша
│   │   ├── Storage/                    // файловое хранилище, учёт места
│   │   └── Secrets/                    // AiKeysOptions, подключение Azure Key Vault
│   │
│   ├── ClipGen.Api/
│   │   ├── Controllers/                // наследуются от ApiControllerBase
│   │   ├── Filters/                    // JobOwnershipFilter
│   │   ├── Middleware/                 // CorrelationId, RequestTiming, обработка исключений
│   │   ├── Jobs/                       // Hangfire-задачи и фильтры
│   │   ├── Progress/                   // SSE, трансляция прогресса
│   │   ├── Setup/                      // установка схемы Hangfire (режим командной строки)
│   │   ├── wwwroot/                    // собранный SPA
│   │   └── Program.cs
│   │
│   └── ClipGen.Admin/                  // Razor Class Library, подключается как Application Part
│       ├── Areas/Admin/Pages/          // Index, Users, Activity
│       ├── Validation/                 // атрибуты + IClientModelValidator
│       └── wwwroot/css/admin.css       // собственные стили, без CSS-фреймворков
│
├── web/clipgen-web/                    // React SPA
└── .github/                            // CI, Dependabot
```

Зависимости: `Api → Admin, Infrastructure, Core`; `Admin → Core, Infrastructure`; `Infrastructure → Core`.

---

## 3. Стек

### 3.1 Backend

| Компонент | Технология |
|---|---|
| Платформа | .NET 10 (LTS), C# 14 |
| Веб-фреймворк | ASP.NET Core 10, Controllers; Minimal API только для health-check и SSE |
| Админ-панель | Razor Pages в Razor Class Library |
| Аутентификация | ASP.NET Core Identity, cookie, роли `Admin` / `User` |
| ORM | EF Core 10 (SQL Server provider), lazy loading proxies — выборочно |
| Микро-ORM | Dapper — отчёты и лента активности в админке |
| База данных | Azure SQL Database |
| Фоновые задачи | Hangfire + Hangfire.SqlServer |
| Секреты | Azure Key Vault: `Azure.Extensions.AspNetCore.Configuration.Secrets`, `Azure.Identity` |
| Data Protection | Ключи в БД (`PersistKeysToDbContext`), защищены ключом Key Vault (`Azure.Extensions.AspNetCore.DataProtection.Keys`) |
| Видео | FFmpeg 7.x через FFMpegCore |
| Транскрипция | Whisper.net + Whisper.net.Runtime.Cuda |
| Дата и время | BCL (`DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `TimeZoneInfo`) + NodaTime |
| HTTP-клиенты | Refit (ElevenLabs), Flurl (Gemini), RestSharp (Anthropic) |
| DI-декораторы | Scrutor |
| Логирование | Serilog с маскированием секретов |

### 3.2 Frontend

| Компонент | Технология |
|---|---|
| Фреймворк | React 19 + TypeScript |
| Сборщик | Vite (в разработке — прокси на API) |
| UI | shadcn/ui + Tailwind CSS |
| Состояние | Zustand (клиентское) + TanStack Query (серверное) |
| Роутинг | React Router 7 |
| HTTP | Axios (`withCredentials`, заголовок anti-forgery) |
| Прогресс | EventSource (SSE) |

---

## 4. Модель данных

### 4.1 Сущности и связи

| Сущность | Связи | Назначение |
|---|---|---|
| `AppUser` (IdentityUser\<Guid\>) | 1 — N `Job`, 1 — N `Tag`, 1 — N `Transcript`, 1 — N `CacheEntry` | Пользователь; + `TimeZoneId`, `CreatedAt`, `LastLoginAt` |
| `Job` | N — 1 `AppUser`, 1 — N `Clip`, N — 1 `Transcript`, **N — N** `Tag`, **N — N** `CacheEntry`, 1 — N `JobRun`, 1 — N `JobStepCheckpoint`, **1 — 1** `JobProgressSnapshot` | Обработка одного видео |
| `Clip` | N — 1 `Job` | Кандидат / готовый клип |
| `Transcript` | N — 1 `AppUser`, 1 — N `Job` | Транскрипт с тайм-кодами слов (JSON); уникален по (`UserId`, `SourceHash`, `Language`), общий для обработок одного видео |
| `Tag` | N — N `Job` (через `JobTag`) | Теги пользователя |
| `JobRun` | N — 1 `Job` | Одна поставленная в Hangfire задача: тип, Hangfire ID, состояние, клипы, ошибка |
| `JobProgressSnapshot` | 1 — 1 `Job` | Последнее состояние прогресса для переподключения SSE |
| `JobStepCheckpoint` | N — 1 `Job` | Завершённые шаги для возобновления |
| `CacheEntry` | N — N `Job` (через `JobCacheEntry`) | Файл кэша (ключ, тип, путь, размер, дата обращения); ссылки от обработок — для безопасного удаления |
| `LoginEvent` | N — 1 `AppUser` (необязательная) | Вход (успешный / неуспешный); `UserName` — введённый логин |
| `DeletionEvent` | N — 1 `AppUser` (необязательная) | Удаление файлов (тип, освобождённый объём); `UserName` — снимок логина |
| `DataProtectionKey` | — | Ключи Data Protection (зашифрованы ключом Key Vault) |

### 4.2 Домен (C#)

```csharp
public enum JobStatus
{
    Uploaded, QueuedAnalysis, Analyzing, AwaitingReview,
    QueuedRender, Rendering, Done, Failed, Cancelled
}

public enum PreviewStatus { NotNeeded, Pending, Ready, Failed }
public enum JobRunKind { Preview, Analyze, Render, Thumbnail }
public enum JobRunState { Enqueued, Processing, Succeeded, Failed, Deleted }

[Flags]
public enum ClipFeatures
{
    None      = 0,
    Subtitles = 1 << 0,
    Narrative = 1 << 1,
    Ducking   = 1 << 2,
}

public enum ClipLength { Sec15 = 15, Sec30 = 30, Sec60 = 60, Sec90 = 90 }
public enum CropMode { Center, BlurredBackground, ManualOffset, SplitScreen }
public enum AudioMix { Silent, Quiet, Original }
public enum NarrativeStyle { Energetic, Neutral, Humorous }
public enum LlmProvider { Gemini, Anthropic }
public enum LengthStatus { Ok, WillSpeedUp, NeedsAction }
public enum ClipSource { Ai, Manual }

// Тайм-коды — значение, а не сущность
public readonly record struct TimeRange(TimeSpan Start, TimeSpan End)
{
    public TimeSpan Duration => End - Start;
    public void Deconstruct(out TimeSpan start, out TimeSpan end) => (start, end) = (Start, End);
}

public readonly record struct Region(int X, int Y, int Width, int Height);   // в пикселях исходника

public sealed record LlmModelRef(LlmProvider Provider, string ModelId);

public sealed record CropSettings(
    CropMode Mode = CropMode.BlurredBackground,
    double Offset = 0.5,              // 0..1, для ManualOffset
    Region? Top = null,               // для SplitScreen
    Region? Bottom = null);

public sealed record SubtitleOptions(bool Enabled = true, string Style = "word-highlight", string Position = "bottom");

public sealed record NarrativeOptions(
    string VoiceId, NarrativeStyle Style, string Language, AudioMix AudioMix, LlmModelRef Model);

public sealed record ProcessingOptions
{
    public ClipLength TargetLength { get; init; } = ClipLength.Sec30;
    public int ClipCount { get; init; } = 5;
    public string AnalysisLanguage { get; init; } = "auto";
    public required LlmModelRef AnalysisModel { get; init; }
    public CropSettings Crop { get; init; } = new();
    public SubtitleOptions Subtitles { get; init; } = new();
    public NarrativeOptions? Narrative { get; init; }

    public ClipFeatures Features =>
        (Subtitles.Enabled ? ClipFeatures.Subtitles : 0)
      | (Narrative is not null ? ClipFeatures.Narrative : 0)
      | (Narrative?.AudioMix is AudioMix.Quiet ? ClipFeatures.Ducking : 0);
}
```

Переопределение настроек на уровне клипа — через nondestructive mutation:

```csharp
var clipOptions = job.Options with { Crop = job.Options.Crop with { Mode = CropMode.ManualOffset, Offset = 0.3 } };
```

Параметры, требующие повторного анализа (`TargetLength`, `ClipCount`, `AnalysisLanguage`, `AnalysisModel`), при изменении через `PUT /api/jobs/{id}/options` в состоянии `AwaitingReview` / `Done` сохраняются, но применяются только при повторном анализе (раздел 6.2).

### 4.3 Сущности EF (сокращённо)

```csharp
public class Job
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string OriginalFileName { get; private set; } = "";
    public string SourceFilePath { get; private set; } = "";
    public string SourceFileHash { get; private set; } = "";     // SHA-256, hex
    public long SourceSizeBytes { get; private set; }
    public bool SourceDeleted { get; private set; }
    public TimeSpan SourceDuration { get; private set; }
    public int SourceWidth { get; private set; }
    public int SourceHeight { get; private set; }
    public double SourceFps { get; private set; }
    public bool SourceHasAudio { get; private set; }
    public PreviewStatus PreviewStatus { get; private set; }
    public string? PreviewFilePath { get; private set; }
    public long? PreviewSizeBytes { get; private set; }
    public Guid? TranscriptId { get; private set; }
    public JobStatus Status { get; private set; }
    public ProcessingOptions Options { get; private set; } = default!;   // JSON-колонка
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public AppUser User { get; private set; } = default!;
    public List<Clip> Clips { get; private set; } = [];
    public virtual Transcript? Transcript { get; private set; }         // lazy loading
    public List<Tag> Tags { get; private set; } = [];
    public List<CacheEntry> CacheEntries { get; private set; } = [];
    public List<JobRun> Runs { get; private set; } = [];
    public List<JobStepCheckpoint> Checkpoints { get; private set; } = [];
    public JobProgressSnapshot? ProgressSnapshot { get; private set; }

    public void MarkQueued(JobRunKind kind) { /* проверка допустимого перехода */ }
    public void UpdateOptions(ProcessingOptions options) { /* инварианты */ }
    // ...
}

public class Clip
{
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public int Index { get; private set; }
    public ClipSource Source { get; private set; }
    public TimeRange Range { get; private set; }                       // complex type: StartTicks, EndTicks
    public int Score { get; private set; }                             // 0..100
    public string Reason { get; private set; } = "";
    public string? Hook { get; private set; }
    public bool IsSelected { get; private set; } = true;
    public CropSettings? CropOverride { get; private set; }            // JSON
    public string? NarrativeText { get; private set; }
    public bool NarrativeEditedManually { get; private set; }
    public string? NarrativeSettingsHash { get; private set; }         // стиль + язык + модель на момент генерации
    public string? NarrationAudioPath { get; private set; }
    public TimeSpan? NarrationDuration { get; private set; }
    public LengthStatus LengthStatus { get; private set; }
    public string? OutputFilePath { get; private set; }                // null до рендера
    public long? OutputSizeBytes { get; private set; }
    public string? ThumbnailPath { get; private set; }
    public DateTimeOffset? RenderedAt { get; private set; }

    public void ApplyTimecodes(TimeRange range, TimeSpan sourceDuration) { /* 1..180 с, в пределах исходника */ }
    // ...
}

public class JobRun
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public JobRunKind Kind { get; set; }
    public string HangfireJobId { get; set; } = "";
    public JobRunState State { get; set; }
    public List<Guid> ClipIds { get; set; } = [];     // для Render: какие клипы рендерить (JSON)
    public string? ErrorMessage { get; set; }
    public DateTimeOffset EnqueuedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}
```

`Clip.NarrativeSettingsHash` отличается от хэша текущих настроек нарратива задачи — в редакторе показывается пометка «Создан с другими настройками».

### 4.4 Правила EF Core

- Конфигурация — только Fluent API (`IEntityTypeConfiguration<T>` на каждую сущность); data annotations используются для валидации DTO, а не для маппинга. Приватные сеттеры маппятся EF штатно.
- `ProcessingOptions`, `CropSettings` — JSON-колонки (`ComplexProperty(...).ToJson()`); `TimeRange` — complex type; `JobRun.ClipIds` — примитивная коллекция (JSON).
- Связи:
  - one-to-many: `User → Jobs`, `Job → Clips`, `Job → Runs`, `Job → Checkpoints` (cascade);
  - many-to-one: `Job → Transcript` (FK `Job.TranscriptId`, `NoAction`) — транскрипт общий для обработок одного видео;
  - one-to-one: `Job ↔ JobProgressSnapshot` (PK снимка = FK на `Job`, cascade);
  - many-to-many: `Job ↔ Tag` через явную `JobTag`, `Job ↔ CacheEntry` через явную `JobCacheEntry`.
- **Каскады только по одной ветке.** SQL Server запрещает несколько каскадных путей к одной таблице, поэтому каскад идёт только `AppUser → Job → (Clip, JobRun, JobTag, JobCacheEntry, JobStepCheckpoint, JobProgressSnapshot)`. Связи `AppUser → Tag`, `AppUser → Transcript`, `AppUser → CacheEntry`, `Tag → JobTag`, `CacheEntry → JobCacheEntry`, `Transcript → Job` — `NoAction`; соответствующие строки удаляются явно в транзакциях удаления обработки и пользователя. `LoginEvent.UserId`, `DeletionEvent.UserId` — `SetNull`: события переживают удаление пользователя.
- Схема управляется EF Migrations; применение миграций — командой `dotnet ef database update` при деплое, не при старте приложения.
- Lazy loading: `UseLazyLoadingProxies(o => o.IgnoreNonVirtualNavigations())` — лениво грузится только `virtual`-навигация `Job.Transcript` (транскрипт большой и нужен не всегда). Все списки загружаются явно через `Include`, чтобы избежать N+1. В Hangfire-задачах транскрипт читается в пределах scope задачи.
- Raw SQL в EF: агрегаты занятого места — `Database.SqlQuery<StorageUsageRow>($"...")` (в EF Core вместо `DbSet.SqlQuery()` из EF6). Файлы кэша с несколькими ссылками учитываются один раз.
- Транзакции: завершение рендера (обновление клипов + статуса задачи), удаление файлов обработки (флаги, ссылки кэша, `DeletionEvent`), полное удаление обработки, удаление пользователя, создание пользователя с ролью. Из-за `EnableRetryOnFailure` (требование для Azure SQL) пользовательские транзакции выполняются внутри `Database.CreateExecutionStrategy().ExecuteAsync(...)`.
- Scaffolding: для админ-сводки состояние очереди читается напрямую из схемы Hangfire через контекст, сгенерированный командой  
  `dotnet ef dbcontext scaffold "<conn>" Microsoft.EntityFrameworkCore.SqlServer --schema HangFire --table HangFire.Job --table HangFire.State --context HangfireReadContext --output-dir Data/HangfireRead`  
  Контекст используется только для чтения; схемой владеет Hangfire.

### 4.5 Dapper

Используется в админ-панели там, где удобнее писать SQL руками:

- **Лента активности** — объединение трёх источников одним запросом; фильтры применяются к объединённому результату:

```sql
WITH Feed AS (
    SELECT 'login'    AS Kind, e.At, e.UserName, CAST(e.Success AS nvarchar(5)) AS Details
    FROM LoginEvents e
    UNION ALL
    SELECT 'job',      j.CreatedAt, u.UserName, j.OriginalFileName
    FROM Jobs j JOIN AspNetUsers u ON u.Id = j.UserId
    UNION ALL
    SELECT 'deletion', d.At, d.UserName, CONCAT(d.Kind, ': ', d.FreedBytes)
    FROM DeletionEvents d
)
SELECT Kind, At, UserName, Details
FROM Feed
WHERE (@UserName IS NULL OR UserName = @UserName)
  AND (@Kind IS NULL OR Kind = @Kind)
  AND At >= @From AND At < @To
ORDER BY At DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
```

`LoginEvents` и `DeletionEvents` хранят логин сами, поэтому попадают в ленту и для несуществующих / удалённых пользователей. `UNION ALL`, а не `UNION`: источники не пересекаются, дедупликация не нужна и только замедлит запрос. `UNION` используется в списке пользователей для фильтра «активные за период» (пользователи с входами ∪ пользователи с обработками — здесь дубликаты нужно убрать).

- **Список обработок пользователя в админке** — метаданные `Job` + `Clip` (имя файла, даты, статус, размеры; без транскриптов и текстов нарратива) одним JOIN с маппингом через `splitOn: "ClipId"`.

---

## 5. Очередь и фоновая обработка — Hangfire

### 5.1 Конфигурация

```csharp
builder.Services.AddHangfire(c => c
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        SchemaName = "HangFire",
        PrepareSchemaIfNecessary = false,                         // схему ставит деплой (5.7)
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),     // возврат задачи после аварии процесса
    })
    .UseFilter(new JobStateSyncFilter())       // синхронизирует JobRun и Job.Status с состоянием Hangfire
    .UseFilter(new JobLoggingFilter()));

// Обработка видео — строго последовательно
builder.Services.AddHangfireServer(o => { o.ServerName = "processing"; o.Queues = ["processing"]; o.WorkerCount = 1; });
// Обслуживание — отдельно, чтобы очистка не ждала длинный рендер
builder.Services.AddHangfireServer(o => { o.ServerName = "maintenance"; o.Queues = ["maintenance"]; o.WorkerCount = 1; });
```

Очередь `default` никто не слушает, поэтому **каждая** задача (и recurring) явно помечена `[Queue("processing")]` или `[Queue("maintenance")]`.

### 5.2 Задачи

| Задача | Тип | Очередь |
|---|---|---|
| `PreviewJob.RunAsync(jobId, runId, ct)` | Фоновая, после загрузки, если исходник не воспроизводится браузером | processing |
| `AnalyzeJob.RunAsync(jobId, runId, ct)` | Фоновая (первичный и повторный анализ) | processing |
| `RenderJob.RunAsync(jobId, runId, ct)` | Фоновая (все выбранные клипы или один клип; список — в `JobRun.ClipIds`) | processing |
| `ThumbnailJob.RunAsync(jobId)` | Continuation после `RenderJob` с `JobContinuationOptions.OnAnyFinishedState` — миниатюры появляются и после частично отменённого рендера | maintenance |
| `CacheCleanupJob.RunAsync()` | Recurring, ежедневно | maintenance |
| `TempCleanupJob.RunAsync()` | Recurring, ежечасно | maintenance |

- Каждая постановка задачи создаёт запись `JobRun`; у одной обработки может быть несколько запусков (анализ, рендер, перерендеры отдельных клипов).
- Имя задачи в дашборде — `[JobDisplayName("Анализ: {0}")]`.
- Для задач обработки `[AutomaticRetry(Attempts = 0)]`: повтор дорогих вызовов AI без участия пользователя нежелателен (раздел 5.4).
- Время recurring-задачи очистки хранится как `TimeOnly` в конфигурации и преобразуется в cron-выражение.

### 5.3 Фильтры

- `JobStateSyncFilter : IApplyStateFilter` — при переходе в `Enqueued` / `Processing` / `Succeeded` / `Failed` / `Deleted` обновляет состояние и `ErrorMessage` соответствующего `JobRun` (по Hangfire ID). Статус обработки пересчитывается по типу запуска:
  - `Analyze`, `Render` — меняют `Job.Status`;
  - `Preview` — меняет только `Job.PreviewStatus`;
  - `Thumbnail` — статусы не трогает, ошибка только логируется.
- `JobLoggingFilter : IServerFilter` — логирует начало, конец и длительность задачи с `JobId` в контексте логов.

### 5.4 Сбои, возобновление и повтор

| Ситуация | Поведение |
|---|---|
| Штатная остановка приложения | Hangfire отменяет `CancellationToken` и возвращает задачу в очередь; после запуска она выполняется снова, шаги с чекпоинтами пропускаются |
| Аварийное завершение процесса | Задача остаётся «невидимой» до истечения `SlidingInvisibilityTimeout` (5 мин), затем снова забирается воркером; шаги с чекпоинтами пропускаются |
| Исключение в шаге | `JobRun` → `Failed` с понятным текстом (`AiProviderException.UserMessage` и т.п.), `Job.Status = Failed`; автоматического повтора нет |
| Повтор пользователем | `POST /api/jobs/{id}/retry` создаёт новый `JobRun` того же типа с теми же параметрами (для рендера — тот же список клипов); завершённые шаги пропускаются по чекпоинтам и кэшу |

### 5.5 Отмена и позиция в очереди

- Отмена: `POST /api/jobs/{id}/cancel` удаляет все активные (`Enqueued` / `Processing`) запуски обработки через `BackgroundJob.Delete(run.HangfireJobId)`. Hangfire отменяет `CancellationToken`, переданный в метод задачи. Токен прокидывается во все шаги: Whisper, HTTP-клиенты, процессы FFmpeg (при отмене процесс завершается `Kill(entireProcessTree: true)`).
- Снятие с очереди работает так же: запуск в состоянии `Enqueued` просто удаляется.
- Позиция в очереди — через `JobStorage.Current.GetMonitoringApi().EnqueuedJobs("processing", 0, 100)`, по первому ожидающему запуску обработки.

### 5.6 Dashboard

- `app.MapHangfireDashboard("/admin/queue", new DashboardOptions { ... })`
- `Authorization = [new AdminDashboardAuthorizationFilter()]` — проверяет роль `Admin` у cookie-пользователя.
- Кастомизация: `DashboardTitle = "ClipGen — очередь"`, `AppPath = "/admin"` (ссылка «назад в админку»), `DisplayStorageConnectionString = false`.

### 5.7 Установка схемы Hangfire

Логин приложения не имеет прав DDL (раздел 21), поэтому схема Hangfire не создаётся при старте (`PrepareSchemaIfNecessary = false`). При деплое, после `dotnet ef database update`, выполняется:

```bash
dotnet ClipGen.Api.dll --install-hangfire-schema --connection "<строка подключения с правами DDL>"
```

В этом режиме `Program.cs` вызывает `SqlServerObjectsInstaller.Install(connection, "HangFire")` и завершается, не запуская веб-сервер.

---

## 6. Пайплайн

### 6.1 Этап 0 — прокси-превью

Сразу после загрузки FFprobe определяет, воспроизводит ли браузер исходник (раздел 8.2). Если нет — `Job.PreviewStatus = Pending` и ставится `PreviewJob` (раздел 8.3). Анализ можно поставить в очередь, не дожидаясь превью: задачи выполняются по порядку.

### 6.2 Этап 1 — анализ

| # | Шаг | Реализация | Кэш / чекпоинт |
|---|---|---|---|
| 1 | Извлечение аудио | FFmpeg → WAV 16 кГц, моно, PCM s16le | Файл по хэшу исходника |
| 2 | Транскрипция | Whisper.net, тайм-коды слов | `Transcript` по пользователю + хэшу + языку |
| 3 | Анализ фрагментов | Выбранная модель LLM; для длинных видео — по чанкам | По хэшу + длина + количество + модель |
| 4 | Черновики нарратива | Выбранная модель LLM, по клипам параллельно | Текст в `Clip` |

**Длинные видео.** Транскрипт делится на чанки по ~20 минут с перекрытием 1 минута. Из каждого чанка запрашиваются кандидаты, затем финальный запрос ранжирует их и выбирает `ClipCount` лучших.

**Постобработка кандидатов:**
1. Парсинг JSON-ответа (очистка от markdown-обёртки регулярным выражением, затем `System.Text.Json` в record).
2. Привязка границ к началу первого и концу последнего слова в диапазоне.
3. Если позволяет запас по длине — сдвиг конца на ближайший конец предложения.
4. Проверка диапазона ±25% (`duration is >= min and <= max`); кандидаты вне диапазона обрезаются или отбрасываются.
5. Удаление пересекающихся кандидатов (остаётся с большим `Score`).

**Параллелизм черновиков нарратива:** `Task.WhenAll` по клипам с ограничением `SemaphoreSlim(3)`.

**Повторный анализ** — тот же `AnalyzeJob` с новым `JobRun`:
- шаги 1–2 берутся из кэша; транскрипция выполняется заново только при смене `AnalysisLanguage`;
- в транзакции удаляются клипы с `Source = Ai` и `OutputFilePath == null`; клипы `Manual` и уже отрендеренные сохраняются;
- новые кандидаты, пересекающиеся с сохранёнными клипами, отбрасываются.

**Генерация текстов нарратива вне анализа** (включение нарратива, смена стиля / языка / модели) — `POST /api/jobs/{id}/narrative/regenerate` с `{ clipIds, overwriteManual }` выполняется синхронно, без очереди (только вызовы LLM, GPU не нужен), `Task.WhenAll` + `SemaphoreSlim(3)`. Клипы с `NarrativeEditedManually` перезаписываются только при `overwriteManual = true`.

### 6.3 Этап 2 — рендер

Шаги прогресса: **1. Генерация озвучки** (если нарратив включён) → **2. Рендер клипа N из M**.

1. **TTS** — для всех выбранных клипов до начала рендера: ElevenLabs, эндпоинт с тайм-кодами символов; кэш по хэшу (текст + голос + стиль + модель TTS). `Task.WhenAll`, `SemaphoreSlim(2)`.
2. **Проверка длины**: фактическая длительность через FFprobe; `ratio = narration / clip`:
   - `ratio <= 1.0` → `LengthStatus.Ok`
   - `ratio is > 1.0 and <= 1.1` → `WillSpeedUp`, в аудиоцепочку добавляется `atempo={ratio}`
   - `ratio > 1.1` → `NeedsAction`: клип исключается из этого рендера, остальные рендерятся; в итоговом SSE-событии — предупреждение `NarrationTooLong`.
3. **Рендер клипа** (шаг «Рендер клипа N из M»): генерация `.ass` из тайм-кодов слов (нарратива — из ElevenLabs с учётом `atempo`, оригинала — из транскрипта), затем один проход FFmpeg: вырезка + кадрирование + субтитры + сведение звука + кодирование (раздел 8).
4. **Сохранение**: в транзакции обновляются клипы и статус задачи.
5. **Continuation** `ThumbnailJob` генерирует миниатюры.

Временные файлы клипа (`.ass`, промежуточные аудио) удаляются в `finally` — независимо от результата, а исключение рендера не теряется:

```csharp
try
{
    await renderer.RenderAsync(plan, ct);
}
finally
{
    workDir.CleanupTemp(clip.Id);
}
```

**Действия для клипа `NeedsAction`** (выполняются в редакторе, затем перерендер одного клипа через `POST /api/clips/{id}/render`):
- «Сократить автоматически» — `POST /api/clips/{id}/narrative/shorten`; новая озвучка генерируется при рендере;
- «Удлинить клип» — SPA вычисляет `end = start + NarrationDuration + 0,3 с`; если `end` выходит за исходник, сдвигается `start`; сохраняется через `PUT /api/clips/{id}` (ограничение 1–180 с). Текст не меняется, поэтому озвучка берётся из кэша;
- «Отредактировать вручную» — `PUT /api/clips/{id}/narrative`.

### 6.4 Расчёт прогресса

Прогресс считается по оценке времени шагов, а не по фиксированным процентам.

- Для каждого шага есть оценка длительности от длины видео `D`: прокси-превью ≈ `D / k_preview`, извлечение аудио ≈ `D / 150`, транскрипция ≈ `D / k_whisper`, анализ ≈ `8 с × число чанков`, нарратив ≈ `4 с × клипы`, озвучка ≈ `3 с × клипы с нарративом`, рендер ≈ `сумма длин клипов / k_render`.
- Коэффициенты `k_preview`, `k_whisper`, `k_render` калибруются по фактической скорости прошлых задач (скользящее среднее, хранится в БД).
- Внутри шага используется реальный прогресс: Whisper — время конца последнего сегмента / длительность; FFmpeg — `out_time` из `-progress` / длительность клипа (или исходника для превью); озвучка — число готовых клипов.
- `percent = (сумма оценок завершённых шагов + оценка текущего × доля выполнения) / сумма всех оценок`.
- ETA = оставшаяся оценка с поправкой на реальную скорость текущего шага.

### 6.5 SSE

```
GET /api/jobs/{id:guid}/progress
Content-Type: text/event-stream

data: {"stage":"preview","status":"Processing","percent":44,"etaSec":70}
data: {"stage":"analysis","status":"QueuedAnalysis","queuePosition":2}
data: {"stage":"analysis","status":"Analyzing","step":"transcribe","stepTitle":"Транскрипция аудио","stepIndex":2,"stepCount":4,"percent":37,"etaSec":412}
data: {"stage":"render","status":"Rendering","step":"tts","stepTitle":"Генерация озвучки","stepIndex":1,"stepCount":2,"percent":12,"etaSec":140}
data: {"stage":"render","status":"Rendering","step":"render","stepTitle":"Рендер клипа","stepIndex":2,"stepCount":2,"clipIndex":2,"clipCount":5,"percent":58,"etaSec":96}
data: {"stage":"render","status":"Done","percent":100,"warnings":[{"clipId":"…","code":"NarrationTooLong"}]}
data: {"stage":"analysis","status":"Failed","error":"Сервис Gemini недоступен. Попробуйте повторить позже."}
```

- Эндпоинт — Minimal API на `TypedResults.ServerSentEvents(...)` (встроено в ASP.NET Core 10), источник — `IAsyncEnumerable<ProgressEvent>`.
- Транслятор прогресса: in-memory, по `Channel<ProgressEvent>` на подписчика; последнее состояние сохраняется в `JobProgressSnapshot` не чаще раза в 5 секунд, чтобы при переподключении сразу отдать актуальный снимок.
- Шаги сообщают прогресс через контравариантный интерфейс (раздел 17).
- Эндпоинт проверяет владение задачей; cookie передаётся `EventSource` автоматически.

---

## 7. Кэширование

Реализовано декораторами, зарегистрированными через Scrutor; бизнес-логика пайплайна о кэше не знает.

```csharp
services.AddScoped<ITranscriptionService, WhisperTranscriptionService>();
services.Decorate<ITranscriptionService, CachingTranscriptionService>();

services.AddScoped<ITtsProvider, ElevenLabsTtsProvider>();
services.Decorate<ITtsProvider, CachingTtsProvider>();

services.AddScoped<IClipAnalyzer, LlmClipAnalyzer>();
services.Decorate<IClipAnalyzer, CachingClipAnalyzer>();
```

| Что | Ключ | Где хранится |
|---|---|---|
| Аудио (WAV) | SHA-256 исходника | Файл в `cache/`, запись `CacheEntry` |
| Транскрипт | Пользователь + SHA-256 + язык | Таблица `Transcript` (уникальный индекс) |
| Кандидаты | SHA-256 + длина + количество + модель анализа | JSON в `CacheEntry` |
| TTS | SHA-256(текст + голос + стиль + модель TTS) | Файл в `cache/`, запись `CacheEntry` |

- Хэш исходника считается потоково при загрузке (`IncrementalHash`), без повторного чтения файла.
- Кэш — в пределах пользователя (`cache/{userId}/...`, `CacheEntry.UserId`, `Transcript.UserId`), чтобы удаление у одного пользователя не влияло на другого.
- **Учёт ссылок.** Декоратор при попадании и при записи связывает `CacheEntry` с текущей обработкой (`JobCacheEntry`, контекст — scoped `IJobContext`) и обновляет `LastAccessedAt`. При «Удалить оригинал» / «Очистить кэш» / «Удалить полностью» удаляется только ссылка этой обработки; файл удаляется, когда ссылок не осталось. Транскрипт удаляется, когда на него не ссылается ни одна обработка.
- Чекпоинты (`JobStepCheckpoint`: JobId, Step, CompletedAt) записываются после каждого шага; при повторном запуске завершённые шаги пропускаются.
- Смена параметров инвалидирует только зависящие от них шаги: ключ кэша каждого шага включает только те параметры, от которых шаг зависит.
- `CacheCleanupJob` удаляет файлы кэша, к которым не обращались дольше срока хранения (`Duration` из NodaTime, по умолчанию 30 дней), **кроме** связанных с обработками, у которых есть активный `JobRun` — очистка из очереди `maintenance` не может удалить файл из-под идущего анализа или рендера. Транскрипты (данные в БД) очисткой не удаляются.

---

## 8. Медиа — FFmpeg

### 8.1 Общие правила

- FFmpeg вызывается через FFMpegCore или `ProcessStartInfo.ArgumentList` — аргументы передаются списком, **никакой склейки командной строки** из пользовательских данных.
- Вырезка: `-ss` перед `-i` с перекодированием — точна до кадра; `-c:v copy` для вырезки не используется (режет по ключевым кадрам и даёт рассинхрон).
- Прогресс: `-progress pipe:2`, значение `out_time_us` извлекается регулярным выражением.
- Граф фильтров собирается построителем `FilterGraphBuilder` (раздел 18), а не строковыми шаблонами.
- Пути в фильтре `ass=` экранируются (двоеточие и обратные слэши в путях Windows).

### 8.2 Совместимость исходника с браузером

По данным FFprobe исходник считается воспроизводимым в Chrome / Edge, если одновременно:
- контейнер — MP4 / MOV (`mov,mp4,m4a,...`) или WebM;
- видеокодек — `h264`, `vp8`, `vp9` или `av1` (HEVC считается невоспроизводимым: поддержка зависит от оборудования);
- аудиокодек — `aac`, `mp3`, `opus`, `vorbis` или звука нет.

Иначе создаётся прокси-превью. Там же определяется `SourceHasAudio`.

### 8.3 Прокси-превью

```bash
ffmpeg -i source.mkv -vf "scale=-2:720,fps=30" -c:v libx264 -preset veryfast -crf 28 \
  -g 30 -keyint_min 30 -c:a aac -b:a 128k -ac 2 -movflags +faststart -progress pipe:2 preview.mp4
```

- Ключевой кадр каждую секунду (`-g 30`) — быстрая и точная перемотка в редакторе.
- Превью используется только плеером; анализ и рендер читают оригинал.
- Области split-screen хранятся в пикселях исходника; SPA пересчитывает координаты из системы координат плеера по `SourceWidth` / `SourceHeight`.

### 8.4 Извлечение аудио для Whisper

```bash
ffmpeg -i source.mp4 -vn -ac 1 -ar 16000 -c:a pcm_s16le -progress pipe:2 audio.wav
```

Для исходника без звука (`SourceHasAudio = false`) шаги извлечения и транскрипции пропускаются, анализ завершается ошибкой с понятным сообщением («В видео нет звука — анализ по речи невозможен»); ручной выбор фрагментов остаётся доступен.

### 8.5 Кадрирование (видеоцепочка)

Все режимы работают при любом соотношении сторон исходника, в том числе вертикальном.

| Режим | Фильтр |
|---|---|
| По центру | `scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920` |
| Размытый фон | `split=2[bg][fg];[bg]scale=270:480:force_original_aspect_ratio=increase,crop=270:480,boxblur=10:2,scale=1080:1920[bgb];[fg]scale=1080:1920:force_original_aspect_ratio=decrease[fgs];[bgb][fgs]overlay=(W-w)/2:(H-h)/2` |
| Ручное смещение | `scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920:x=(iw-ow)*{offset}:y=(ih-oh)/2` |
| Split-screen | `split=2[a][b];[a]crop={w1}:{h1}:{x1}:{y1},scale=1080:960:force_original_aspect_ratio=increase,crop=1080:960[top];[b]crop={w2}:{h2}:{x2}:{y2},scale=1080:960:force_original_aspect_ratio=increase,crop=1080:960[bottom];[top][bottom]vstack` |

- «Размытый фон»: фон размывается в уменьшенном размере и затем растягивается — результат визуально тот же, рендер заметно быстрее; передний план вписывается целиком (`decrease`), поэтому не выходит за кадр и у вертикальных исходников.
- «Ручное смещение»: для исходника не шире 9:16 `iw = ow`, смещение ни на что не влияет — слайдер в UI скрыт.

Далее к цепочке добавляются `fps=30`, `ass=subs.ass` (если субтитры включены), `format=yuv420p`.

### 8.6 Сведение звука (аудиоцепочка)

Нормализация во всех режимах: `loudnorm=I=-14:TP=-1.5:LRA=11`.

Вход оригинала `[orig]` — `[0:a]`, а для исходника без звука — дополнительный вход `-f lavfi -i anullsrc=r=48000:cl=stereo`. Озвучка дополняется тишиной (`apad`), итоговая длительность ограничивается выходным `-t {duration}` и `amix ... duration=first`, поэтому звуковая дорожка всегда равна длине клипа.

| Режим | Фильтр |
|---|---|
| Без нарратива | `[orig]loudnorm=…[aout]` |
| Нарратив, оригинал выключен | `[1:a]atempo={t},apad,loudnorm=…[aout]` |
| Нарратив, тихий фон (ducking) | `[1:a]atempo={t},apad[narr];[orig]volume='{envelope}':eval=frame[duck];[duck][narr]amix=inputs=2:normalize=0:duration=first,loudnorm=…[aout]` |
| Нарратив, оригинал сохранён | `[1:a]atempo={t},apad[narr];[orig][narr]amix=inputs=2:normalize=0:duration=first,loudnorm=…[aout]` |

- `atempo` добавляется только при `LengthStatus.WillSpeedUp`.
- `amix` всегда с `normalize=0`, иначе громкость входов делится и нарратив становится тише.

**Огибающая ducking.** Интервалы речи нарратора точно известны из тайм-кодов ElevenLabs (с поправкой на `atempo`), поэтому громкость оригинала задаётся явно, а не компрессором (у `sidechaincompress` глубина ослабления зависит от громкости голоса и не даёт стабильных 8%):
- соседние реплики с паузой меньше 0,4 с объединяются в один интервал;
- внутри интервала громкость 0,08 (≈ −22 дБ), переходы — линейные, 0,15 с до начала и после конца;
- `FilterGraphBuilder` строит выражение `1-0.92*max(r1, max(r2, …))`, где для интервала `[a, b]`  
  `ri = clip((t-(a-0.15))/0.15,0,1)*clip(((b+0.15)-t)/0.15,0,1)`.

### 8.7 Финальный рендер (один проход)

```bash
ffmpeg -ss {start} -t {duration} -i source.mp4 [-i narration.mp3] [-f lavfi -i anullsrc=r=48000:cl=stereo] \
  -filter_complex "{видеоцепочка}[vout];{аудиоцепочка}" \
  -map "[vout]" -map "[aout]" -t {duration} \
  -r 30 -c:v libx264 -profile:v high -preset medium -pix_fmt yuv420p \
  -b:v 10M -minrate 10M -maxrate 10M -bufsize 20M \
  -c:a aac -ar 48000 -ac 2 -b:a 192k \
  -movflags +faststart -progress pipe:2 \
  clip_{id}.mp4
```

### 8.8 Субтитры (.ass)

- Генерируются из тайм-кодов слов; стили — секции `[V4+ Styles]` на каждый пресет.
- «Слово за словом с подсветкой» — одна фраза на экране, текущее слово выделяется цветом через теги `{\c&H…&}` с переключением по тайм-кодам.
- Безопасные зоны TikTok: `MarginV` не меньше 320 px снизу для позиции «низ», `MarginR` не меньше 140 px; позиция «центр» / «верх» — через `Alignment`.
- Для нарратива тайм-коды символов ElevenLabs собираются в слова и делятся на коэффициент `atempo`.

### 8.9 FFprobe

- Метаданные исходника (длительность, разрешение, FPS, контейнер, кодеки, наличие звука) и длительность озвучки.
- JSON от ffprobe оборачивается в `ProbeData : DynamicObject` (раздел 17) — доступ к полям вида `probe.format.duration` без описания всей схемы ffprobe классами.

---

## 9. Транскрипция — Whisper.net

```csharp
using var factory = WhisperFactory.FromPath(settings.ModelPath);   // ggml-large-v3 (или large-v3-turbo)

using var processor = factory.CreateBuilder()
    .WithLanguage(language)           // "auto" | "ru" | "en" | ...
    .WithTokenTimestamps()            // тайм-коды на уровне токенов — нужны для субтитров «слово за словом»
    .SplitOnWord()
    .Build();

await foreach (var segment in processor.ProcessAsync(wavStream, ct))
{
    // segment.Start / segment.End — сегмент; segment.Tokens — токены с собственными тайм-кодами
    // токены склеиваются в слова → List<Word(Text, Start, End)>
    progress.Report(new TranscriptionProgress(segment.End, totalDuration));
}
```

- Среда: `Whisper.net.Runtime.Cuda`; модель скачивается один раз и хранится локально.
- Перед обработкой проверяется заголовок WAV (частота 16 кГц, моно, 16 бит) — чтением структуры `WavHeader` (раздел 17).
- Результат сохраняется в `Transcript.WordsJson`: `[{ "t": "слово", "s": 12.34, "e": 12.71 }, ...]`.

---

## 10. AI-провайдеры

### 10.1 Абстракции

```csharp
public interface ILlmProvider
{
    LlmProvider Id { get; }
    bool IsConfigured { get; }        // ключ задан в Key Vault
    Task<IReadOnlyList<LlmModelInfo>> ListModelsAsync(CancellationToken ct);
    Task<string> CompleteAsync(LlmRequest request, CancellationToken ct);
}

public interface ITtsProvider
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<VoiceInfo>> ListVoicesAsync(CancellationToken ct);
    Task<TtsResult> SynthesizeAsync(TtsRequest request, CancellationToken ct);  // аудио + тайм-коды символов
}
```

| Реализация | HTTP-клиент | Паттерн |
|---|---|---|
| `GeminiProvider : ILlmProvider` | Flurl | Adapter |
| `AnthropicProvider : ILlmProvider` | RestSharp | Adapter |
| `ElevenLabsTtsProvider : ITtsProvider` | Refit | Adapter |

`LlmProviderResolver` выбирает реализацию по `LlmModelRef.Provider`.

### 10.2 Ключи

Ключи приходят из Azure Key Vault через конфигурацию (раздел 21) и читаются через `IOptionsMonitor<AiKeysOptions>` **на каждый запрос** — ключ, изменённый администратором в Key Vault, начинает использоваться после очередного перечитывания конфигурации (≤ 5 минут), без перезапуска.

```csharp
public sealed class AiKeysOptions
{
    public ProviderKey Gemini { get; set; } = new();
    public ProviderKey Anthropic { get; set; } = new();
    public ProviderKey ElevenLabs { get; set; } = new();
}
public sealed class ProviderKey { public string? ApiKey { get; set; } }
```

Отсутствующий или пустой секрет = провайдер не настроен (`IsConfigured = false`); соответствующие функции в UI недоступны.

### 10.3 Выбор моделей в UI

- `GET /api/models` опрашивает `ListModelsAsync` всех настроенных провайдеров параллельно. Каждый запрос оборачивается продолжением, которое превращает ошибку провайдера в пометку «недоступен», чтобы сбой одного не ломал весь список:

```csharp
var tasks = providers.Where(p => p.IsConfigured).Select(p =>
    p.ListModelsAsync(ct).ContinueWith(t => t.IsCompletedSuccessfully
            ? ProviderModels.Available(p.Id, t.Result)
            : ProviderModels.Unavailable(p.Id),
        CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default));

var result = await Task.WhenAll(tasks);
```

- Список фильтруется до моделей генерации текста и кэшируется в памяти на 1 час.
- Выбранные модели сохраняются в `ProcessingOptions` задачи; значения по умолчанию нет — модель выбирается явно.
- Конкретные идентификаторы моделей в коде не зашиты.

### 10.4 HTTP-клиенты

**Refit — ElevenLabs:**

```csharp
public interface IElevenLabsApi
{
    [Get("/v1/voices")]
    Task<VoicesResponse> GetVoicesAsync(CancellationToken ct);

    [Post("/v1/text-to-speech/{voiceId}/with-timestamps")]
    Task<TtsWithTimestampsResponse> SynthesizeAsync(
        string voiceId, [Body] TtsRequest body, [Query] string output_format, CancellationToken ct);
}
```

Заголовок `xi-api-key` добавляется `DelegatingHandler`-ом, который берёт ключ из `IOptionsMonitor<AiKeysOptions>.CurrentValue` на каждый запрос. Сериализация — `SystemTextJsonContentSerializer` с snake_case.

**Flurl — Gemini:** базовые настройки клиента (`FlurlHttp.Clients.WithDefaults`): таймаут, сериализатор, заголовок `x-goog-api-key` через обработчик `BeforeCall` (ключ из `IOptionsMonitor`); ошибки — `FlurlHttpException` с разбором тела ответа.

**RestSharp — Anthropic:** `RestClient` с базовым URL и заголовком `anthropic-version`; `x-api-key` добавляется к каждому `RestRequest` из `IOptionsMonitor`; `RestRequest` с JSON-телом; ответ через встроенную десериализацию `ExecutePostAsync<MessagesResponse>`.

**Общее для всех трёх:**
- Ошибки логируются без тела запроса (в нём может быть весь транскрипт) — только статус, провайдер, модель, длительность.
- Повтор при 429 / 5xx — до 3 раз с экспоненциальной задержкой (это повтор HTTP-вызова внутри шага, а не повтор задачи Hangfire).
- Таймаут вызова LLM — `Task.WhenAny(call, Task.Delay(timeout, ct))`: если первой завершилась задержка, связанный `CancellationTokenSource` отменяет вызов и пользователь получает понятную ошибку.
- Все ошибки провайдеров приводятся к `AiProviderException(provider, code, userMessage)`; 401/403 → «Неверный или отсутствующий API-ключ, обратитесь к администратору».

### 10.5 Промпты

**Анализ фрагментов:**
```
Ты помогаешь создавать контент для TikTok.
Транскрипт видео, формат строк: [начало–конец в секундах] текст

{transcript}

Выбери {clipCount} фрагментов. Длина каждого — от {minSec} до {maxSec} секунд (целевая {targetSec}).
Требования к фрагменту:
- законченная мысль или история;
- в первые 1–3 секунды звучит интригующая фраза (hook);
- понятен без контекста остального видео;
- начинается и заканчивается на границе фразы, не обрывает слово.
Оцени каждый фрагмент от 0 до 100 по вероятности удержать зрителя.

Верни ТОЛЬКО JSON без пояснений:
[{"start_sec": 0, "end_sec": 0, "score": 0, "reason": "...", "hook": "..."}]
Поле reason — на языке: {uiLanguage}.
```

**Генерация нарратива:**
```
Ты TikTok-комментатор. Стиль: {styleDescription}.
Язык ответа: {narrativeLanguage}.
Фрагмент видео [{start} – {end}], его содержание:

{transcriptFragment}

Напиши закадровый комментарий. Жёсткое ограничение: не более {targetWords} слов
(это ~{targetSec} секунд речи). Разговорный, живой, с реакциями.
Не пересказывай дословно — добавь свой взгляд.
Верни ТОЛЬКО текст комментария.
```
`targetWords = floor(duration × 0.9 × 2.5)`; `styleDescription` задаётся для каждого значения `NarrativeStyle`.

**Сокращение нарратива** (действие «сократить автоматически»):
```
Сократи текст до {targetWords} слов, сохранив смысл, стиль и язык. Верни ТОЛЬКО текст.

{narrativeText}
```

Транскрипт — пользовательские данные: он всегда передаётся как содержимое, промпт требует строго JSON, ответ валидируется по схеме; инструкции внутри транскрипта не могут изменить формат ответа.

---

## 11. Файлы и загрузка

### 11.1 Структура хранилища

```
{Storage:Root}/
├── users/{userId}/
│   ├── jobs/{jobId}/
│   │   ├── source/source.{ext}
│   │   ├── source/preview.mp4       // прокси-превью, если исходник не воспроизводится браузером
│   │   ├── clips/{clipId}.mp4
│   │   ├── clips/{clipId}.jpg
│   │   └── work/                 // временные файлы (.ass, промежуточное аудио)
│   └── cache/                    // общий для обработок пользователя, учёт ссылок — JobCacheEntry
│       ├── audio/{sha256}.wav
│       └── tts/{sha256}.mp3
└── tmp/                          // незавершённые загрузки
```

### 11.2 Загрузка

- Потоковый приём multipart через `MultipartReader` без `IFormFile` (файл не буферизуется в памяти), запись сразу в `tmp/`.
- Лимиты сняты точечно на эндпоинте: `[DisableRequestSizeLimit]`, `[RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]`.
- Во время записи параллельно считается SHA-256 (`IncrementalHash`).
- Проверка сигнатуры по первым байтам, а не по расширению: MP4/MOV — `ftyp` на смещении 4; AVI — `RIFF....AVI `; MKV/WEBM — `1A 45 DF A3`.
- Во время записи сервер следит за свободным местом (`DriveInfo`); при нехватке загрузка прерывается, временный файл удаляется, клиент получает `ProblemDetails` «Недостаточно места на диске». Предварительного показа свободного места в UI нет.
- Имя файла пользователя используется только как отображаемое `OriginalFileName` (после санитизации); путь на диске строится из GUID.
- После загрузки — FFprobe для метаданных и проверки совместимости (раздел 8.2), перенос в `source/`, при необходимости — постановка `PreviewJob`.

### 11.3 Воспроизведение исходника в редакторе

`GET /api/jobs/{id}/source` → `PhysicalFile(path, mime, enableRangeProcessing: true)`: плеер перематывает по Range-запросам, не скачивая файл целиком.
- `PreviewStatus = NotNeeded` → отдаётся оригинал;
- `Ready` → отдаётся `preview.mp4`;
- `Pending` → `409 Conflict` с `ProblemDetails` «Превью готовится»; SPA ждёт события SSE `stage: preview`;
- `Failed` → `ProblemDetails` с текстом ошибки; повтор — `POST /api/jobs/{id}/retry`.

### 11.4 Учёт и удаление

- Размеры файлов хранятся в БД (`SourceSizeBytes`, `PreviewSizeBytes`, `OutputSizeBytes`, `CacheEntry.SizeBytes`); занятое место — SQL-агрегат, без обхода диска.
- Удаление: одна транзакция в БД — флаги, обнуление путей, удаление ссылок `JobCacheEntry`, удаление осиротевших `CacheEntry` / `Transcript`, запись `DeletionEvent` с фактически освобождаемым объёмом. Это единственное место записи аудита удаления. После коммита — физическое удаление файлов. Если удаление файла не удалось, путь попадает в список на повторное удаление в `TempCleanupJob`.
- Освобождаемый объём для диалога подтверждения считается тем же запросом, что и удаление (без файлов, на которые остаются ссылки).

---

## 12. API

Все маршруты с идентификаторами — с ограничением `{id:guid}`. Ошибки — `ProblemDetails`. Неаутентифицированный запрос к `/api/*` получает 401, запрос без прав — 403 (без редиректа).

### Аутентификация и профиль

| Метод | Путь | Описание |
|---|---|---|
| GET | /api/auth/antiforgery | Выдаёт anti-forgery токен (cookie `XSRF-TOKEN`) |
| POST | /api/auth/login | Вход |
| POST | /api/auth/logout | Выход |
| GET | /api/auth/me | Текущий пользователь, роль, часовой пояс |
| PUT | /api/profile/timezone | Сменить часовой пояс |
| PUT | /api/profile/password | Сменить пароль |

### Задачи

| Метод | Путь | Описание |
|---|---|---|
| POST | /api/jobs | Загрузка видео (потоковый multipart) → задача `Uploaded`, при необходимости — `PreviewJob` |
| GET | /api/jobs | История текущего пользователя (`?tag=&page=`) |
| GET | /api/jobs/{id} | Детали + клипы + последний запуск (статус, ошибка) |
| PUT | /api/jobs/{id}/options | Параметры обработки |
| PUT | /api/jobs/{id}/tags | Теги задачи |
| POST | /api/jobs/{id}/analyze | Поставить анализ в очередь (первичный или повторный) |
| POST | /api/jobs/{id}/render | Поставить рендер выбранных клипов в очередь (`{ clipIds: [] }`) |
| POST | /api/jobs/{id}/cancel | Отменить / снять с очереди все активные запуски |
| POST | /api/jobs/{id}/retry | Повторить последний упавший запуск с теми же параметрами |
| POST | /api/jobs/{id}/narrative/regenerate | Сгенерировать / перегенерировать тексты нарратива (`{ clipIds, overwriteManual }`), синхронно |
| GET | /api/jobs/{id}/progress | SSE (Minimal API) |
| GET | /api/jobs/{id}/transcript | Транскрипт с тайм-кодами слов |
| GET | /api/jobs/{id}/source | Видео для плеера (оригинал или прокси-превью) с поддержкой Range |
| GET | /api/jobs/{id}/download-all | ZIP готовых клипов (потоковая сборка) |
| DELETE | /api/jobs/{id}/source | Удалить оригинал (и превью) |
| DELETE | /api/jobs/{id}/clip-files | Удалить отрендеренные клипы |
| DELETE | /api/jobs/{id}/cache | Очистить кэш |
| DELETE | /api/jobs/{id} | Удалить полностью |

Для DELETE-эндпоинтов `?dryRun=true` возвращает освобождаемый объём без удаления (для диалога подтверждения).

### Клипы

| Метод | Путь | Описание |
|---|---|---|
| POST | /api/jobs/{id}/clips | Добавить ручной фрагмент |
| PUT | /api/clips/{id} | Тайм-коды, `IsSelected`, кадрирование (без рендера) |
| DELETE | /api/clips/{id} | Удалить ручной фрагмент |
| PUT | /api/clips/{id}/narrative | Сохранить текст нарратива |
| POST | /api/clips/{id}/narrative/regenerate | Перегенерировать текст (опционально — другая модель) |
| POST | /api/clips/{id}/narrative/shorten | Сократить текст под длину клипа |
| POST | /api/clips/{id}/render | Перерендер одного клипа (в очередь) |
| GET | /api/clips/{id}/download | Скачать MP4 |
| GET | /api/clips/{id}/thumbnail | Миниатюра |

### Справочники

| Метод | Путь | Описание |
|---|---|---|
| GET | /api/models | Доступные модели по настроенным провайдерам |
| GET | /api/voices | Голоса ElevenLabs (если ключ задан) |
| GET | /api/tags | Теги пользователя |
| GET | /api/storage/usage | Занятое место пользователя |
| GET | /health | Health-check (Minimal API, без авторизации) |

Админ-панель (`/admin/...`) — Razor Pages, не REST (раздел 20).

---

## 13. Безопасность

| Требование | Реализация |
|---|---|
| Авторизация | Глобальная политика «аутентифицирован» (`FallbackPolicy`); `[Authorize(Policy = "AdminOnly")]` и `AuthorizeAreaFolder("Admin", "/", "AdminOnly")` для админки. Анонимный доступ — только login, antiforgery, health, статические файлы SPA и fallback `index.html` (`MapFallbackToFile(...).AllowAnonymous()`, иначе неавторизованный пользователь не получит даже страницу входа) |
| Ответы без авторизации | Cookie-события: для путей `/api` `OnRedirectToLogin` → 401, `OnRedirectToAccessDenied` → 403; для остальных — редирект на `LoginPath = "/login"` (SPA) с `returnUrl` |
| Доступ к чужим данным | `JobOwnershipFilter` на всех эндпоинтах с `{id}` задачи/клипа — в том числе для администраторов; запросы к БД всегда с условием `UserId` |
| Данные пользователей в админке | Только метаданные (имя файла, даты, статусы, размеры, ошибки); транскрипты, тексты нарратива и файлы других пользователей админке недоступны |
| Пароли | ASP.NET Core Identity (хэширование, политика сложности, блокировка после 5 неудачных попыток) |
| Секреты | API-ключи AI-сервисов и строка подключения — в Azure Key Vault; администратор задаёт их вручную. Приложение аутентифицируется в Key Vault как service principal по сертификату из хранилища сертификатов машины (роль `Key Vault Secrets User` — только чтение секретов). В конфигурационных файлах и репозитории секретов нет: в `appsettings.json` только URI хранилища, tenant ID, client ID и отпечаток сертификата |
| Data Protection | Ключи хранятся в БД (`PersistKeysToDbContext`) и зашифрованы ключом `dataprotection-kek` в Key Vault (`ProtectKeysWithAzureKeyVault`, роль `Key Vault Crypto User`) |
| Шифрование | Только штатные механизмы (Data Protection, Identity, Key Vault); собственная криптография не пишется |
| Доступ к БД | Только параметризованные запросы: EF LINQ, интерполированный `SqlQuery`, параметры Dapper |
| Ввод | Валидация DTO (раздел 14); сигнатура загружаемого файла; санитизация имён файлов; пути только из GUID |
| Вывод | Экранирование Razor и React по умолчанию; текст AI и транскрипта никогда не вставляется как HTML |
| Внешние процессы | Аргументы FFmpeg — списком (`ArgumentList`) |
| Логи | Serilog-деструктуризация маскирует свойства `*Key*`, `*Password*`, `*Token*`, `*ConnectionString*`; значения из Key Vault и тела запросов к AI не логируются |
| Исключения | `IExceptionHandler` → `ProblemDetails` без стектрейса вне Development; детали — только в логах с correlation ID |
| HTTPS | `UseHttpsRedirection` + `UseHsts` вне Development; cookie `Secure`, `HttpOnly`, `SameSite=Strict` |
| CORS | Не используется: в production SPA на том же origin, в разработке — через прокси Vite (раздел 19.4) |
| CSRF | SPA: anti-forgery токен в cookie `XSRF-TOKEN` → заголовок `X-XSRF-TOKEN`, проверка `[AutoValidateAntiforgeryToken]` на изменяющих запросах. Токен привязан к пользователю, поэтому после входа и выхода SPA заново запрашивает `/api/auth/antiforgery`. Razor — встроенные токены форм |
| Hangfire Dashboard | Только роль Admin (`IDashboardAuthorizationFilter`) |

---

## 14. Валидация

- **Model binding** — DTO как `record` с атрибутами; `[ApiController]` автоматически возвращает 400 с `ValidationProblemDetails`, формат ответа настроен через `ApiBehaviorOptions.InvalidModelStateResponseFactory`.
- **`ModelStateDictionary`** — в действиях, где ошибки добавляются после бизнес-проверок (например, `ModelState.AddModelError("clipIds", "Клип не принадлежит задаче")`), и во всех Razor-формах админки.
- **Встроенные атрибуты** — `[Required]`, `[Range(1, 10)]` для `ClipCount`, `[StringLength]`, `[EnumDataType]`.
- **Кастомные атрибуты:**
  - `[ClipRange]` — `EndSec > StartSec`, длина от 1 до 180 секунд, не за пределами исходника;
  - `[UserLogin]` — формат логина (латиница, цифры, `._-`, 3–32 символа) и запрет зарезервированных имён (`admin`, `root`, `system`, …); реализует `IClientModelValidator`, добавляя `data-val-*` атрибуты для клиентской валидации в форме создания пользователя в админке.
- **`IValidatableObject`** — `ProcessingOptionsDto`: модель анализа обязательна; голос и модель нарратива обязательны, если нарратив включён; области split-screen обязательны при режиме `SplitScreen` и лежат в пределах исходника; смещение 0..1 только при `ManualOffset`; выбранные модели принадлежат настроенным провайдерам.
- **Razor-админка** — tag helpers `asp-for`, `asp-validation-for`, `asp-validation-summary`; клиентская валидация — jQuery Validation Unobtrusive с адаптером для `UserLogin`.
- **Frontend** — дублирующая валидация форм на клиенте для удобства; источник истины — сервер.

---

## 15. Переиспользование кода и роутинг

### 15.1 Переиспользование

- **DI** — все сервисы через контейнер; регистрации сгруппированы в методах-расширениях (`AddClipGenSecrets()`, `AddClipGenInfrastructure()`, `AddClipGenAi()`, `AddClipGenPipeline()`).
- **Базовый контроллер** — `ApiControllerBase : ControllerBase` с `[ApiController]`, `[Authorize]`, свойством `CurrentUserId` и хелпером `ProblemFor(AppError)`.
- **Middleware** — `CorrelationIdMiddleware` (ID запроса в заголовке и контексте логов), `RequestTimingMiddleware` (логирует медленные запросы), обработка исключений через `IExceptionHandler`.
- **Filters** — `JobOwnershipFilter` (`IAsyncActionFilter`, через `[ServiceFilter]`).
- **Сквозная функциональность** — логирование, кэширование и проверка владения вынесены из бизнес-кода в middleware, фильтры и декораторы; аудит удаления — часть транзакции удаления (раздел 11.4).
- **Application Parts** — админка живёт в `ClipGen.Admin` (Razor Class Library) и подключается явно:

```csharp
builder.Services.AddRazorPages()
    .AddApplicationPart(typeof(ClipGen.Admin.AdminAssemblyMarker).Assembly);
```

### 15.2 Роутинг и порядок middleware

```csharp
app.UseExceptionHandler();
app.UseHsts();
app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();    // кастомный middleware — до сопоставления маршрутов
app.UseStaticFiles();                             // до авторизации: ассеты SPA доступны без входа
app.UseRouting();                                 // сопоставление маршрута явно после кастомного middleware
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseMiddleware<RequestTimingMiddleware>();     // после UseRouting — видит выбранный endpoint и его метаданные

app.MapControllers();
app.MapRazorPages();
app.MapHangfireDashboard("/admin/queue", dashboardOptions);
app.MapGet("/health", () => Results.Ok()).AllowAnonymous();              // endpoint-делегат
app.MapGet("/api/jobs/{id:guid}/progress", ProgressEndpoints.StreamAsync); // SSE
app.MapFallbackToFile("index.html").AllowAnonymous();                    // маршруты SPA, включая /login
```

- Атрибутные шаблоны маршрутов в контроллерах (`[Route("api/jobs")]`, `[HttpGet("{id:guid}")]`).
- Ссылки на ресурсы (например, `Location` после создания задачи) — через `LinkGenerator` / `CreatedAtAction`.

---

## 16. Регулярные выражения

Все выражения — в `ClipGen.Core/Text/Patterns.cs` через source generator `[GeneratedRegex]` (современная замена `Regex.CompileToAssembly`, которая в .NET Core не поддерживается: регулярное выражение компилируется в сборку на этапе сборки).

| Назначение | API | Детали |
|---|---|---|
| Прогресс FFmpeg (`out_time_us=…`) | `Match` + `Groups` | Именованная группа `us` |
| Длительность в выводе FFmpeg (`Duration: 00:12:34.56`) | `Match` + `Groups` | Группы `h`, `m`, `s`, `cs` → `TimeSpan` |
| Разбивка транскрипта на предложения | `Regex.Split` | По `[.!?…]+\s+` с сохранением разделителя |
| Очистка ответа LLM от markdown-обёртки | `Regex.Replace` | Удаление `` ```json `` / `` ``` `` |
| Санитизация имени файла | `Regex.Replace` | Недопустимые символы → `_` |
| Логин пользователя | статический `Regex.IsMatch` | Латиница, цифры, `._-`, 3–32 символа; в атрибуте `[UserLogin]` |
| Имя тега | статический `Regex.IsMatch` | Буквы, цифры, пробел, дефис |

Настройка движка:
- Все выражения, обрабатывающие внешние данные (ответы LLM, вывод FFmpeg, ввод пользователя), — с `matchTimeoutMilliseconds` (100–500 мс).
- Для выражений над большими текстами (транскрипт) — `RegexOptions.NonBacktracking` (линейное время, защита от катастрофического бэктрекинга).
- `RegexOptions.CultureInvariant`, `ExplicitCapture` там, где не нужны безымянные группы.
- Для одного выражения, собираемого в рантайме (поиск по транскрипту в редакторе, если будет добавлен), — `RegexOptions.Compiled` с кэшированием экземпляра.

---

## 17. Язык: ООП, асинхронность, продвинутые возможности, дата и время

### 17.1 Типы

| Тип | Где | Почему |
|---|---|---|
| `readonly record struct` | `TimeRange`, `Region`, `Word` | Маленькие неизменяемые значения, сравнение по значению, без аллокаций |
| `sealed record` | `ProcessingOptions`, `CropSettings`, DTO API | Неизменяемые данные, `with`-копирование |
| `class` | Сущности EF, сервисы | Изменяемое состояние, идентичность, прокси lazy loading |
| `interface` | `ILlmProvider`, `ITtsProvider`, `ITranscriptionService`, `IVideoRenderer`, `IPipelineStep` | Подмена реализаций, декораторы |
| `abstract class` | `PipelineStepBase` | Общая логика шага: чекпоинт, замер времени, отчёт о прогрессе |

Инкапсуляция: сеттеры `Job` и `Clip` — `private`, изменение через методы (`job.MarkQueued(kind)`, `job.UpdateOptions(...)`, `clip.ApplyTimecodes(range, sourceDuration)`) с проверкой инвариантов (раздел 4.3); внутренние типы Infrastructure — `internal`.

### 17.2 Вариантность

```csharp
// Контравариантность: репортер общих событий подходит там, где нужен репортер конкретных
public interface IProgressReporter<in TEvent> where TEvent : ProgressEvent
{
    void Report(TEvent e);
}
// IProgressReporter<ProgressEvent> → IProgressReporter<TranscriptionProgress>

// Ковариантность: результат конкретного шага можно обработать как результат общего типа
public interface IStepResult<out TOutput>
{
    TOutput Output { get; }
    TimeSpan Elapsed { get; }
}
// IStepResult<TranscriptResult> → IStepResult<object> для общего логирования

// Вариантный делегат
public delegate void ProgressHandler<in TEvent>(TEvent e) where TEvent : ProgressEvent;
```

### 17.3 Асинхронность

| Возможность | Где |
|---|---|
| `async`/`await`, `Task<T>` | Весь I/O: HTTP, БД, FFmpeg, Whisper |
| `Task.Run` | CPU-bound: генерация `.ass`, склейка токенов Whisper в слова для длинных транскриптов, построение огибающей ducking |
| `ContinueWith` | Опрос моделей у провайдеров: продолжение превращает сбой провайдера в пометку «недоступен» (раздел 10.3) |
| `try` / `finally` | Очистка временных файлов после рендера без потери исключения (раздел 6.3) |
| `Task.WhenAll` | TTS и тексты нарратива по клипам; опрос моделей у провайдеров |
| `Task.WhenAny` | Таймаут вызова LLM (раздел 10.4) |
| Результат `Task<T>` | Через `await` (в продолжении — `t.Result` уже завершённой задачи); `.Result` / `.Wait()` на незавершённых задачах в коде приложения запрещены — в ASP.NET Core они блокируют поток пула и ведут к исчерпанию потоков |
| `CancellationToken` | Во всех асинхронных методах; источник — отмена Hangfire и отключение клиента SSE |

### 17.4 Продвинутые возможности языка

| Возможность | Где |
|---|---|
| `[Flags]` + битовые операции | `ClipFeatures`: сборка через `\|`, проверки `(features & ClipFeatures.Narrative) != 0`; сдвиги `1 << n` в объявлении |
| `StructLayout` | `[StructLayout(LayoutKind.Sequential, Pack = 1)] struct WavHeader` — чтение заголовка WAV через `MemoryMarshal.Read<WavHeader>` |
| `dynamic` + `DynamicObject` | `ProbeData` — обёртка над JSON ffprobe с доступом `probe.streams[0].codec_name` |
| `ExpandoObject` | Payload SSE-события: набор полей зависит от этапа, сериализуется как JSON-объект |
| Records + `with` | Переопределение опций на уровне клипа |
| Деконструкция | `var (start, end) = clip.Range;`, кортежи `(ratio, status) = CheckLength(...)` |
| Логические паттерны | `ratio is > 1.0 and <= 1.1`, `status is JobStatus.Done or JobStatus.Failed`, `duration is >= min and <= max`, `not null` |

### 17.5 Дата и время

| Задача | Тип / API |
|---|---|
| Моменты событий (`CreatedAt`, `LastLoginAt`, `At`) | `DateTimeOffset` в UTC; в Azure SQL — `datetimeoffset` |
| Тайм-коды клипов | `TimeSpan` |
| Группировка истории по дням | `DateOnly` в часовом поясе пользователя |
| Время ежедневной очистки | `TimeOnly` → cron |
| Часовой пояс пользователя | IANA-идентификатор; список зон и конвертация в админке — `TimeZoneInfo.GetSystemTimeZones()`, `TimeZoneInfo.ConvertTime` |
| Относительное время («2 дня назад») | NodaTime `Period.Between(...)` в зоне пользователя (`DateTimeZoneProviders.Tzdb`) |
| Срок хранения кэша | NodaTime `Duration` |
| Формат дат в API | ISO 8601 (`InstantPattern.ExtendedIso`) |
| Парсинг времени из вывода FFmpeg | `TimeSpan.ParseExact` |

---

## 18. Структурные паттерны

| Паттерн | Где |
|---|---|
| **Decorator** | Кэширующие декораторы над транскрипцией, TTS, анализом (раздел 7) |
| **Adapter** | `GeminiProvider`, `AnthropicProvider`, `ElevenLabsTtsProvider` приводят разные API к единым интерфейсам |
| **Facade** | `VideoRenderer` скрывает FFmpeg, FFprobe, построение графа фильтров и генерацию `.ass` за методом `RenderAsync(RenderPlan)` |
| **Composite** | `FilterGraphBuilder`: узлы-фильтры и составные цепочки (`scale → crop → ass`, выражение огибающей ducking) собираются в дерево и сериализуются в строку `-filter_complex` |
| **Bridge** | Режимы кадрирования (`ICropStrategy`: Center, BlurredBackground, ManualOffset, SplitScreen) независимы от способа рендера |
| **Proxy** | Lazy loading proxies EF для `Job.Transcript` |

---

## 19. Frontend

### 19.1 Страницы

```
/login               → LoginPage
/                    → UploadPage (загрузка + параметры)
/job/:id             → ProcessingPage (очередь / прогресс превью, анализа или рендера; ошибка и «Повторить»)
/job/:id/editor      → EditorPage (кандидаты, транскрипт, тайм-коды, нарратив, параметры, повторный анализ)
/job/:id/results     → ResultsPage (готовые клипы, пропущенные клипы, скачивание)
/history             → HistoryPage (фильтр по тегам, группировка по дням)
/profile             → ProfilePage (часовой пояс, пароль)
```

### 19.2 Компоненты

```
src/
├── pages/
├── components/
│   ├── upload/        DropZone, UploadProgress, PreviewPreparing
│   ├── options/       OptionsForm, ModelSelector, VoiceSelector, CropSelector, SplitScreenRegionPicker, SubtitleOptions
│   ├── processing/    ProgressTracker, QueuePosition, FailedRunNotice (кнопка «Повторить»)
│   ├── editor/        CandidateList, CandidateCard, SourcePreviewPlayer, TimecodeEditor, CropOffsetSlider,
│   │                  NarrativeEditor, NarrationLengthIndicator, NarrationTooLongActions, TranscriptViewer,
│   │                  Timeline, ManualClipSelector, EditorOptionsPanel, ReanalyzeDialog, RegenerateNarrativesDialog
│   ├── results/       ClipCard, ClipPlayer, SkippedClipCard, DownloadAllButton
│   ├── history/       HistoryList, TagFilter, TagInput, StorageUsage, DeleteMenu
│   └── shared/        VideoPlayer, ConfirmDialog
├── stores/            jobStore.ts, editorStore.ts
├── api/               client.ts (axios + XSRF), endpoints/*.ts
└── hooks/             useJobProgress.ts (SSE), useNotifications.ts
```

### 19.3 Редактор

- `SourcePreviewPlayer` проигрывает отрезок видео из `/api/jobs/{id}/source` (оригинал или прокси-превью): `currentTime = start`, остановка по событию `timeupdate` при достижении `end`. Пока превью готовится, вместо плеера — статус подготовки.
- `SplitScreenRegionPicker` пересчитывает области из координат плеера в пиксели исходника (`SourceWidth` / `SourceHeight`).
- `CropOffsetSlider` скрыт, если исходник не шире 9:16.
- `NarrationLengthIndicator` оценивает длительность по числу слов (2,5 слова/с) до генерации озвучки, после рендера — по фактической.
- `NarrationTooLongActions` — три действия из продуктовой спецификации (раздел 2.6; реализация — раздел 6.3) и кнопка «Отрендерить клип».
- `EditorOptionsPanel` показывает параметры; изменение параметров анализа активирует «Повторить анализ» (`ReanalyzeDialog` с числом заменяемых кандидатов), изменение настроек нарратива — пометки «Создан с другими настройками» и `RegenerateNarrativesDialog`.
- Тайм-коды в UI — секунды с десятыми.

### 19.4 Разработка: прокси Vite

```ts
// vite.config.ts
export default defineConfig({
  server: {
    port: 3000,
    proxy: {
      '/api': { target: 'https://localhost:5001', secure: false },
    },
  },
});
```

Браузер обращается только к `http://localhost:3000`, поэтому cookie аутентификации и `XSRF-TOKEN` работают как в production (Chrome считает `localhost` безопасным контекстом и принимает `Secure`-cookie). SSE проксируется тем же правилом.

---

## 20. Админ-панель (Razor Pages)

| Страница | Содержимое |
|---|---|
| `/admin` | Сводка: занятое / свободное место, пользователи, состояние очереди (через `HangfireReadContext`), доступность AI-сервисов (ключ задан / не задан — по `AiKeysOptions`, без вывода значений) |
| `/admin/users`, `/admin/users/create`, `/admin/users/{id}` | Список (с фильтром «активные за период»), создание, редактирование, блокировка, удаление (все обработки и файлы пользователя — в транзакции, затем физическое удаление; журнал сохраняется) |
| `/admin/activity` | Лента активности (Dapper, UNION ALL), фильтры, пагинация |
| `/admin/queue` | Hangfire Dashboard |

- Layout — `_AdminLayout.cshtml` с внешним стилем `/_content/ClipGen.Admin/css/admin.css`; inline-стили не используются.
- **Стили пишутся вручную**, без CSS-фреймворков:
  - тёмная тема на CSS custom properties (`--bg`, `--surface`, `--text`, `--accent`);
  - `box-sizing: border-box` глобально; отступы карточек через `gap` во flex/grid-контейнерах, чтобы избежать схлопывания вертикальных margin; там, где блоки идут потоком (текстовые секции), схлопывание учтено осознанно;
  - единицы: `rem` для шрифтов и отступов, `%`/`fr` для колонок, `ch` для ширины полей ввода, `vh` для высоты области ленты, `px` только для рамок;
  - переполнение: таблицы в контейнере `overflow-x: auto`; длинные логины и имена файлов — `text-overflow: ellipsis` с `title`; лента активности — фиксированная высота и `overflow-y: auto`.
- Формы — tag helpers, anti-forgery токены добавляются автоматически, клиентская валидация — jQuery Validation Unobtrusive (подключается локально из RCL).

---

## 21. Конфигурация

**appsettings.json** (без секретов):

```json
{
  "KeyVault": {
    "Uri": "https://clipgen-kv.vault.azure.net/",
    "TenantId": "<tenant-id>",
    "ClientId": "<client-id сервисного principal>",
    "CertificateThumbprint": "<отпечаток сертификата в LocalMachine\\My>",
    "ReloadMinutes": 5
  },
  "Storage": { "Root": "D:/clipgen" },
  "Whisper": { "ModelPath": "D:/clipgen/models/ggml-large-v3.bin" },
  "FFmpeg": { "BinaryFolder": "C:/ffmpeg/bin" },
  "Processing": { "NarrativeParallelism": 3, "TtsParallelism": 2, "LlmTimeoutSeconds": 120 },
  "Maintenance": { "CacheRetentionDays": 30, "DailyCleanupTime": "03:00" }
}
```

### Azure Key Vault

Секреты задаются администратором вручную (портал Azure или `az keyvault secret set`); `--` в имени секрета соответствует `:` в ключе конфигурации.

| Секрет | Ключ конфигурации | Обязателен |
|---|---|---|
| `ConnectionStrings--Default` | `ConnectionStrings:Default` | Да |
| `Ai--Gemini--ApiKey` | `Ai:Gemini:ApiKey` | Нет — без него модели Gemini недоступны |
| `Ai--Anthropic--ApiKey` | `Ai:Anthropic:ApiKey` | Нет — без него модели Claude недоступны |
| `Ai--ElevenLabs--ApiKey` | `Ai:ElevenLabs:ApiKey` | Нет — без него нарратив недоступен |

Ключ (не секрет) `dataprotection-kek` (RSA 2048) — для защиты ключей Data Protection.

```csharp
var kv = builder.Configuration.GetSection("KeyVault").Get<KeyVaultSettings>()!;
TokenCredential credential = builder.Environment.IsDevelopment()
    ? new AzureCliCredential()                                       // разработчик: az login
    : new ClientCertificateCredential(kv.TenantId, kv.ClientId, LoadCertificate(kv.CertificateThumbprint));

builder.Configuration.AddAzureKeyVault(new Uri(kv.Uri), credential,
    new AzureKeyVaultConfigurationOptions { ReloadInterval = TimeSpan.FromMinutes(kv.ReloadMinutes) });

builder.Services.Configure<AiKeysOptions>(builder.Configuration.GetSection("Ai"));

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>()
    .ProtectKeysWithAzureKeyVault(new Uri(new Uri(kv.Uri), "keys/dataprotection-kek"), credential);
```

Права (Azure RBAC):
- service principal приложения — `Key Vault Secrets User` и `Key Vault Crypto User` (чтение секретов, wrap/unwrap ключа);
- администратор — `Key Vault Secrets Officer` (создание и изменение секретов);
- для разработки — отдельное хранилище `clipgen-kv-dev` с тестовыми ключами.

Отключение или удаление секрета AI-сервиса в Key Vault после перечитывания делает соответствующий провайдер недоступным.

### Azure SQL

- Тариф: General Purpose Serverless с минимальными vCore и **отключённой авто-паузой**. Hangfire постоянно обращается к БД (опрос очереди, heartbeat серверов), поэтому база с ним всё равно не засыпала бы, а попытка её усыпить только добавляла бы задержку «пробуждения».
- Сетевой доступ: firewall-правило для внешнего IP домашней сети; публичный доступ для остальных адресов закрыт.
- Аутентификация: SQL-логин приложения с минимальными правами (`db_datareader`, `db_datawriter`, права на выполнение в схеме HangFire); строка подключения с этим логином — в Key Vault. Миграции EF и установка схемы Hangfire (раздел 5.7) выполняются отдельным логином с правами DDL; его строка подключения есть только у администратора на время деплоя и в Key Vault приложения не попадает.
- Шифрование соединения: `Encrypt=True`.

---

## 22. Внешние инструменты

| Инструмент | Версия | Назначение |
|---|---|---|
| .NET SDK | 10.x | Сборка и запуск |
| FFmpeg + FFprobe | 7.x, со сборкой libx264 и libass | Обработка видео, субтитры, прокси-превью |
| NVIDIA драйвер + CUDA | Версия согласно требованиям `Whisper.net.Runtime.Cuda` | GPU-транскрипция |
| Модель Whisper | ggml-large-v3 или large-v3-turbo (~1,5–3 ГБ) | Транскрипция |
| Node.js | 22 LTS+ | Сборка frontend |
| Azure CLI | 2.x | Управление секретами Key Vault, вход разработчика |

---

## 23. CI и безопасность кода (GitHub Actions)

Тесты не предусмотрены; CI проверяет сборку и безопасность.

| Проверка | Инструмент | Когда |
|---|---|---|
| Сборка backend | `dotnet build -warnaserror` | Каждый push / PR |
| Сборка frontend | `npm ci && npm run build` (включая `tsc`) | Каждый push / PR |
| Уязвимые NuGet-пакеты | `dotnet list package --vulnerable --include-transitive` — падение при High/Critical | Каждый push / PR |
| Уязвимые npm-пакеты | `npm audit --audit-level=high` | Каждый push / PR |
| Статический анализ (SAST) | GitHub CodeQL для C# и JavaScript/TypeScript | PR + еженедельно |
| Анализаторы .NET | `<AnalysisLevel>latest-recommended</AnalysisLevel>`, `EnableNETAnalyzers` | При сборке |
| Обновления зависимостей | Dependabot: NuGet, npm, GitHub Actions | Еженедельно |
| Секреты в репозитории | GitHub secret scanning + push protection | Всегда |

Найденные проблемы (инъекции, нарушения контроля доступа, небезопасная работа с путями) исправляются до слияния PR.

---

## 24. Ограничения и компромиссы

| Ограничение | Причина | Возможное развитие |
|---|---|---|
| Face tracking | Сложность | MediaPipe в v2 |
| Одна задача одновременно | Одна GPU | Отдельная очередь для рендера, если CPU простаивает во время транскрипции |
| Нужен интернет | БД, Key Vault и AI в облаке | — |
| Azure SQL без авто-паузы | Hangfire постоянно опрашивает БД | Хранилище Hangfire в локальной БД |
| Прокси-превью для части форматов | Браузер не воспроизводит часть кодеков | Воспроизведение через WebCodecs / MSE |
| Анализ только по транскрипту | Видео без речи анализируются плохо | Сигналы громкости и смены сцен |
| Нет тестов | Решение по скоупу | Добавить при росте проекта |
| Загрузка одним запросом | Простота | Chunked-загрузка с докачкой для файлов в десятки ГБ |

---

## Приложение A. Покрытие пунктов матрицы компетенций

| Пункт | Где в проекте | Статус |
|---|---|---|
| Reuses and encapsulates related data and behavior | 17.1, 17.2, 4.3 | ✅ |
| Implements asynchrony (Key) | 17.3, 6.3, 10.3, 10.4 | ✅ `Wait()` — сознательно не используется, с обоснованием |
| Regular expressions | 16 | ✅ |
| Advanced language capabilities | 17.4 | ✅ |
| Date and time with language APIs | 17.5 | ✅ |
| ORM (Key) — EF Core | 4.4 (миграции, scaffolding, Fluent API, связи 1—1 / 1—N / N—N, транзакции) | ✅ |
| ORM — Dapper | 4.5 (`splitOn`) | ⚠️ частично: схемой управляют EF Migrations, FluentMigrator и FluentMap не используются |
| ORM — NHibernate | — | ➖ не используется (альтернатива EF Core) |
| Date and time with libraries | 17.5 (NodaTime) | ✅ |
| Analytics (GA) | — | ➖ не используется |
| Hangfire | 5 | ✅ |
| Quartz.NET | — | ➖ не используется (альтернатива Hangfire) |
| HTTP clients — Refit | 10.4 | ✅ |
| HTTP clients — RestSharp | 10.4 | ✅ |
| HTTP clients — Flurl | 10.4 | ⚠️ частично: мокирование через `HttpTest` не покрыто, так как тестов нет |
| Framework security (Key) | 13 | ✅ |
| Reuses code using framework (Key) | 15.1 | ✅ |
| DB engines (Key) — EF Core | 4.4 (LINQ, DbContext, lazy loading, raw SQL) | ✅ |
| DB engines — Dapper | 4.5 | ✅ |
| Advanced navigation (routing) | 15.2 | ✅ |
| Validates data with framework (Key) | 14 | ✅ |
| CSS: styling template, units, overflow, basic properties, box model | 20 | ✅ |
| Structural design patterns (Key) | 18 | ✅ |
| Combines multiple queries (Key) | 4.5 (UNION ALL / UNION) | ✅ |
| Code security (Key) | 23, 13 | ✅ |
| Stores data in a cloud database (Key) | 21 (Azure SQL) | ✅ |
| Router / API / message queue in a cloud | — | ❌ не покрываются: приложение локальное (решение по архитектуре); Azure Key Vault используется только как хранилище секретов |
