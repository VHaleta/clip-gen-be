using ClipGen.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClipGen.Be.IntegrationTests.Infrastructure;

public sealed class DatabaseFixture : IAsyncLifetime
{
    public CustomWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        // A separate DbContext unrelated to the host: the migration must create the database BEFORE
        // WebApplicationFactory starts the app (and app startup, including role seeding
        // in Program.cs, already happens on the first access to Factory.Services/CreateClient()).
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
