using ClipGen.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClipGen.Be.IntegrationTests.Infrastructure;

public sealed class DatabaseFixture : IAsyncLifetime
{
    public CustomWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        // Отдельный, не связанный с хостом DbContext: миграция должна создать базу ДО того,
        // как WebApplicationFactory запустит приложение (а запуск приложения, включая сид ролей
        // в Program.cs, стартует уже при первом обращении к Factory.Services/CreateClient()).
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(CustomWebApplicationFactory.ResolveConnectionString(), sql => sql.EnableRetryOnFailure())
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await Factory.DisposeAsync();
}

[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
