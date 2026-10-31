using System.Data;
using ClipGen.Infrastructure.Data;
using ClipGen.Infrastructure.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClipGen.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddClipGenInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Строка подключения 'ConnectionStrings:Default' не задана.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        // Отдельное соединение для Dapper (read-путь отчётов) — на той же строке подключения,
        // без привязки к change tracking EF Core.
        services.AddScoped<IDbConnection>(_ => new SqlConnection(connectionString));
        services.AddScoped<JobReportsQueries>();

        return services;
    }
}
