# Этапы реализации и прогресс

Текущий блок работы: backend-слой Jobs/Clips/Tags (EF Core + Dapper, JWT, Swagger). Дизайн и обоснование решений —
в `docs/data-layer-plan.md`. Этот файл отслеживает только статус этапов.

| # | Этап | Статус |
|---|---|---|
| 1 | Скаффолдинг решения — проекты `ClipGen.Core`, `ClipGen.Infrastructure`, `tests/ClipGen.Be.IntegrationTests`, ссылки, `clip-gen-be.slnx`, удаление WeatherForecast | Готово |
| 2 | Доменная модель (`ClipGen.Core`) — сущности, enum'ы, value objects | Готово |
| 3 | Персистентность (`ClipGen.Infrastructure`) — `AppDbContext`, Fluent-конфигурации, DI-расширение, Dapper-запрос отчёта | Готово |
| 4 | Первая миграция и применение к LocalDB (`ClipGenDb`) | Готово |
| 5 | Identity + JWT (`ClipGen.Be.Host`) — `TokenService`, `AuthController`, сид ролей/демо-админа | Готово |
| 6 | Доменные контроллеры — `JobsController`, `ClipsController`, `TagsController`, ownership-фильтры, DTO и валидационные атрибуты | Готово |
| 7 | Admin-отчёт — `AdminController` + политика `AdminOnly` + Dapper-путь | Готово |
| 8 | Swagger — security scheme, описание, финальная сборка `Program.cs` и пайплайна middleware | Готово |
| 9 | Интеграционные тесты — фабрика/фикстура + 6 тестовых классов | Не начато |
| 10 | Техническая заметка `docs/data-access-notes.md` — trade-off'ы EF Core/Dapper + реальные результаты тестов | Не начато |
| 11 | Финальная проверка — `dotnet build`, `dotnet ef database update`, ручной проход через Swagger, `dotnet test`, обновление статусов здесь | Не начато |

Обновляется по ходу работы: статус меняется на «В работе» при старте этапа и на «Готово» по завершении.
