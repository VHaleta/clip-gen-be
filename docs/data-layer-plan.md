# План: backend-блок Jobs/Clips/Tags (EF Core + Dapper, JWT, Swagger)

## Контекст

Задача — покрыть кодом 4 пункта матрицы компетенций одним цельным, рабочим через Swagger куском бэкенда:
«Store data in a cloud database», «Use integration test approaches», «Apply security practices with framework»,
«Use engines for database interaction».

Сейчас в репозитории только дефолтный шаблон `ClipGen.Be.Host` (WeatherForecast). Полные product-spec/tech-spec
описывают весь продукт (видео, FFmpeg, Hangfire, AI) — это не нужно для данного блока. Берём только часть модели
данных из tech-spec раздел 4 (`AppUser`, `Job`, `Clip`, `Tag`), убираем всё, что связано с обработкой файлов, и
достраиваем до отдельного, самодостаточного, демонстрируемого через Swagger UI куска: хранение в SQL-базе через
EF Core (CRUD, миграции, транзакции, JSON/complex-type колонки, связи 1–N и N–N), один read-путь через Dapper
(для демонстрации trade-off между ORM и micro-ORM), Identity-авторизация с ролями Admin/User через JWT, и
интеграционные тесты, бьющие по реальному HTTP API и реальной SQL-базе.

Договорённости с пользователем:
- Домен: Standard — `Job + Clip + Tag`.
- Структура решения: слои по tech-spec — `ClipGen.Core` / `ClipGen.Infrastructure` / `ClipGen.Be.Host`.
- Аутентификация: ASP.NET Core Identity (хранение пользователей/ролей) + JWT Bearer (не cookie/antiforgery —
  проще тестировать через Swagger: один токен через кнопку Authorize, без ручного проставления XSRF-заголовка).
- БД: сейчас LocalDB локально; инфраструктура должна поддерживать переключение на Azure SQL без изменения кода —
  только заменой строки подключения в конфигурации (`EnableRetryOnFailure` включается всегда, на LocalDB это no-op).
- Code-first/Migrations — схема создаётся из кода (`dotnet ef database update`), ручных SQL-скриптов нет.

## Решение

### 1. `src/ClipGen.Core` (class library, net10.0)

`Domain/`:
- `AppUser : IdentityUser<Guid>` — минимально, плюс `CreatedAt`.
- `Job` — `Id, UserId, OriginalFileName, SourceFileHash, SourceDuration (TimeSpan), Status (JobStatus), Options
  (ProcessingOptions), CreatedAt, UpdatedAt`; приватные сеттеры; методы `Job.Create(...)`, `UpdateOptions(...)`,
  `Transition(JobStatus)` (проверка допустимых переходов), `AddClip(...)` — инкапсуляция по образцу tech-spec 17.1.
  Навигации: `List<Clip> Clips`, `List<Tag> Tags` (через `JobTag`).
- `Clip` — `Id, JobId, Index, Source (ClipSource: Ai/Manual), Range (TimeRange), Score, Reason, Hook?, IsSelected,
  NarrativeText?, CreatedAt, UpdatedAt`.
- `Tag` — `Id, UserId, Name` (уникален в рамках пользователя).
- `JobTag` — явная join-сущность (`JobId, TagId` — составной PK), а не неявная skip-navigation.
- `Enums.cs` — `JobStatus {Draft, AwaitingReview, Done, Cancelled}`, `ClipSource {Ai, Manual}`,
  `ClipLength {Sec15=15, Sec30=30, Sec60=60, Sec90=90}`, `CropMode {Center, BlurredBackground, ManualOffset}`.
- `ValueObjects.cs` — `TimeRange` (`readonly record struct`, complex type, не сущность), `CropSettings`
  (`CropMode Mode, double Offset`), `ProcessingOptions` (`ClipLength TargetLength, int ClipCount, string
  AnalysisLanguage, CropSettings Crop`) — хранится как JSON-колонка.

Никаких зависимостей от Infrastructure/ASP.NET, кроме `Microsoft.AspNetCore.Identity` (только ради `IdentityUser<Guid>`).

### 2. `src/ClipGen.Infrastructure`

- `Data/AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>`.
- `Data/Configurations/*Configuration.cs` — `IEntityTypeConfiguration<T>` для `Job`, `Clip`, `Tag`, `JobTag`:
  - `Job`: `builder.ComplexProperty(j => j.Options).ToJson();` (JSON-колонка, вложенный `CropSettings` внутри);
    каскад только по ветке `AppUser → Job → (Clip, JobTag)`, остальное `NoAction` (аналог правила tech-spec 4.4
    про единственную каскадную ветку).
  - `Clip`: `builder.ComplexProperty(c => c.Range, r => {...})` (complex type, не JSON — два простых столбца
    `RangeStart`/`RangeEnd`); `HasOne` → `Job`, cascade.
  - `Tag`: уникальный индекс `(UserId, Name)`.
  - `JobTag`: составной ключ `(JobId, TagId)`, `NoAction` на `Tag → JobTag` (тег может использоваться другой
    обработкой — не удаляется каскадно).
- `Reports/JobReportsQueries.cs` — Dapper, `IDbConnection` (Microsoft.Data.SqlClient), один SQL-запрос JOIN
  `Jobs + AspNetUsers + Clips (COUNT) + Tags (STRING_AGG)` → `JobSummaryRow[]`, читает все задачи всех
  пользователей (для админ-отчёта), без текста нарратива/файлов — только метаданные.
- `DependencyInjection.cs` — `AddClipGenInfrastructure(IConfiguration)`: регистрирует `AppDbContext` через
  `UseSqlServer(connStr, o => o.EnableRetryOnFailure())`, `IDbConnection` фабрику для Dapper на той же строке.
- `Migrations/` — создаются командой (см. «Проверка»).

NuGet: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`,
`Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Dapper`, `Microsoft.Data.SqlClient`.

### 3. `ClipGen.Be.Host` (существующий проект)

- Удалить `Controllers/WeatherForecastController.cs`, `WeatherForecast.cs` (мёртвый код шаблона).
- `Program.cs`:
  - `AddClipGenInfrastructure(builder.Configuration)`.
  - `AddIdentityCore<AppUser>(...).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()` — только модель пользователей/ролей, без cookie-схемы.
  - `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` — валидация issuer/audience/
    signing key из конфигурации (`Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey`, `Jwt:ExpiryMinutes`); роли из
    токена маппятся на `ClaimTypes.Role`, чтобы работали `[Authorize(Roles=...)]`/policy.
  - `AddAuthorizationBuilder().AddPolicy("AdminOnly", p => p.RequireRole("Admin"))`.
  - `AddSwaggerGen(...)` (Swashbuckle.AspNetCore) — `SecurityDefinition("Bearer", Http/bearer/JWT)` + глобальный
    `SecurityRequirement`, чтобы кнопка Authorize в Swagger UI принимала «вставить токен» без лишних шагов;
    `OpenApiInfo.Description` — краткая инструкция (`register → login → скопировать token → Authorize`).
  - Сид ролей `Admin`/`User` и (только в Development, из `User Secrets` `Seed:AdminUserName`/`Seed:AdminPassword`,
    если заданы) одного демо-админа — через `IHost` startup-скоуп.
  - Пайплайн: `UseExceptionHandler → UseRouting → UseAuthentication → UseAuthorization → MapControllers`,
    `UseSwagger/UseSwaggerUI`, `MapGet("/health", ...).AllowAnonymous()`.
  - `partial class Program` в конце файла — чтобы `WebApplicationFactory<Program>` в тестах видел тип (стандартный
    приём для top-level statements).
- `Auth/TokenService.cs` — генерация JWT (`System.IdentityModel.Tokens.Jwt`): claims `NameIdentifier`, `Name`,
  по одному `Role` на роль; подпись `SymmetricSecurityKey` (HMAC SHA256) из `Jwt:SigningKey`.
- `Controllers/ApiControllerBase.cs` — `[ApiController][Authorize]`, `CurrentUserId` (из `ClaimTypes.NameIdentifier`).
- `Controllers/AuthController.cs` — `POST /api/auth/register` (создаёт пользователя, роль `User`),
  `POST /api/auth/login` (`UserManager.CheckPasswordAsync` → `TokenService` → `{ token, expiresAt }`),
  `GET /api/auth/me` (данные из claims текущего токена). Logout не нужен — токен stateless, клиент просто
  перестаёт его использовать.
- `Controllers/JobsController.cs` — `POST /api/jobs`, `GET /api/jobs`, `GET /api/jobs/{id}`,
  `PUT /api/jobs/{id}/options`, `PUT /api/jobs/{id}/tags` (заменяет набор тегов — демонстрирует запись N–N),
  `DELETE /api/jobs/{id}` (одна транзакция: клипы, `JobTag`, сам `Job`).
- `Controllers/ClipsController.cs` — `POST /api/jobs/{id}/clips`, `PUT /api/clips/{id}`, `DELETE /api/clips/{id}`.
- `Controllers/TagsController.cs` — `GET /api/tags`.
- `Controllers/AdminController.cs` — `GET /api/admin/jobs-summary`, `[Authorize(Policy = "AdminOnly")]`, через
  `JobReportsQueries` (Dapper).
- `Filters/JobOwnershipFilter.cs`, `Filters/ClipOwnershipFilter.cs` — `IAsyncActionFilter`, регистрируются как
  `Scoped`, применяются через `[ServiceFilter(typeof(...))]`; 404, если задача/клип не принадлежит текущему
  пользователю (чтобы не раскрывать существование чужих данных).
- `Contracts/` — DTO-record'ы с `[Required]`, `[Range(1,10)]` (`ClipCount`), `[EnumDataType]`;
  `ProcessingOptionsDto : IValidatableObject` (офсет кадрирования валиден только при `ManualOffset`);
  `[ClipRange]` атрибут (`EndSec > StartSec`, длительность 1–180 сек) на DTO клипа;
  `[UserLogin]` атрибут на `RegisterRequest.UserName` (латиница/цифры/`._-`, 3–32 симв., запрет `admin/root/system`).
- `appsettings.json` — `"ConnectionStrings": { "Default": "" }` (пусто — явно требует конфигурации в не-Dev
  окружении). `appsettings.Development.json` — реальная строка на LocalDB:
  `Server=(localdb)\MSSQLLocalDB;Database=ClipGenDb;Trusted_Connection=True;TrustServerCertificate=True;`.
  Переключение на Azure SQL позже — замена этой строки (и добавление `User Id/Password` или managed identity),
  без изменений в коде.
  `Jwt:Issuer`/`Jwt:Audience`/`Jwt:ExpiryMinutes` — в `appsettings.json` (не секреты). `Jwt:SigningKey` — пусто в
  `appsettings.json`, реальное dev-значение через `dotnet user-secrets` (это секрет: утечка позволяет подделывать
  токены, в отличие от беспарольной LocalDB-строки).

NuGet (Host): `Swashbuckle.AspNetCore`, `Microsoft.AspNetCore.Authentication.JwtBearer`,
`System.IdentityModel.Tokens.Jwt`.

### 4. `tests/ClipGen.Be.IntegrationTests` (xUnit + `Microsoft.AspNetCore.Mvc.Testing`)

- `CustomWebApplicationFactory.cs` — `WebApplicationFactory<Program>`, переопределяет `ConnectionStrings:Default`
  на `Server=(localdb)\MSSQLLocalDB;Database=ClipGenDb_Test;...` (константа; при необходимости — override через
  переменную окружения `CLIPGEN_TEST_CONNECTION`, что пригодится при переходе на Azure SQL).
- `DatabaseFixture.cs` (collection fixture) — один раз мигрирует тестовую БД (`Database.Migrate()`).
- Тесты (каждый создаёт свои уникальные данные через `/api/auth/register`, чистит за собой в `DisposeAsync`):
  1. `AuthFlowTests` — register → login (получить JWT) → `/api/auth/me` с токеном → запрос без токена/с
     испорченным токеном даёт 401.
  2. `JobStorageTests` — создать `Job` с `ProcessingOptions` (включая `CropSettings`), получить обратно,
     сверить все поля — round-trip через JSON-колонку (EF Core).
  3. `ClipAndCascadeTests` — добавить клипы к `Job`, удалить `Job`, убедиться что клипы удалены (каскад/транзакция).
  4. `TagSharingTests` — один тег на двух `Job` одного пользователя, удалить один `Job` — тег остаётся
     (демонстрация `NoAction` + явного N–N).
  5. `OwnershipBoundaryTests` — пользователь B не может читать/менять/удалять `Job` пользователя A (404).
  6. `AdminReportTests` — обычный пользователь получает 403 на `/api/admin/jobs-summary`; админ (сид из
     Development-конфигурации) получает корректную агрегацию (Dapper-путь).

### 5. `docs/data-access-notes.md`

Короткая техническая заметка (не инструкция, а результат):
- EF Core vs Dapper — почему запись/транзакционные операции через EF Core (change tracking, миграции,
  навигации), а кросс-пользовательский admin-отчёт — через Dapper (ручной JOIN/агрегация без избыточных
  `Include`, но без compile-time проверки и без change tracking).
- Итоги прогона интеграционных тестов: что покрыли (auth, CRUD, каскады, N–N, граница владения, роль admin),
  какие дефекты нашли и исправили по ходу (заполнится по факту прогона).

### Обновить `clip-gen-be.slnx`

Добавить все новые проекты (`ClipGen.Core`, `ClipGen.Infrastructure`, `ClipGen.Be.IntegrationTests`).

## Проверка

1. `dotnet build` — решение собирается.
2. `dotnet ef migrations add InitialCreate --project src/ClipGen.Infrastructure --startup-project ClipGen.Be.Host`
   → `dotnet ef database update --project src/ClipGen.Infrastructure --startup-project ClipGen.Be.Host` — создаёт
   `ClipGenDb` на LocalDB из кода.
3. `dotnet run --project ClipGen.Be.Host`, открыть `/swagger` — пройти вручную: `POST /api/auth/register` →
   `POST /api/auth/login` (скопировать `token` из ответа) → кнопка Authorize (вставить токен) → `POST /api/jobs` →
   `GET /api/jobs/{id}` → `POST .../clips` → `PUT /api/jobs/{id}/tags` → `DELETE /api/jobs/{id}`.
4. `dotnet test tests/ClipGen.Be.IntegrationTests` — все 6+ тестов зелёные на `ClipGenDb_Test` (LocalDB).
5. Зафиксировать реальные результаты шага 4 в `docs/data-access-notes.md`.

Этапы реализации и прогресс трекаются в `docs/plan.md`.
