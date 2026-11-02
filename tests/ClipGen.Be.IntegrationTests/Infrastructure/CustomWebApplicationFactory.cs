using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ClipGen.Be.IntegrationTests.Infrastructure;

/// <summary>
/// A dedicated DB (not the developer's Development one) and a dedicated dev JWT key keep the tests self-contained
/// and independent of personal User Secrets. CLIPGEN_TEST_CONNECTION lets you specify a different
/// connection string (e.g. Azure SQL after moving off LocalDB) without changing the test code.
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
