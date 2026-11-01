using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ClipGen.Be.IntegrationTests.Infrastructure;

/// <summary>
/// Своя БД (не та, что у разработчика в Development) и свой dev-ключ JWT — тесты самодостаточны
/// и не зависят от персональных User Secrets. CLIPGEN_TEST_CONNECTION позволяет указать другую
/// строку подключения (например, Azure SQL после перехода с LocalDB) без изменения кода тестов.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminUserName = "test-admin";
    public const string AdminPassword = "Test#Admin2026";

    public const string DefaultTestConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=ClipGenDb_Test;Trusted_Connection=True;TrustServerCertificate=True;";

    public static string ResolveConnectionString() =>
        Environment.GetEnvironmentVariable("CLIPGEN_TEST_CONNECTION") ?? DefaultTestConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:Default", ResolveConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-tests-only-signing-key-not-a-real-secret-0123456789");
        builder.UseSetting("Seed:AdminUserName", AdminUserName);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
    }
}
