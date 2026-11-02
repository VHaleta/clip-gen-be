using ClipGen.Core.Abstractions.Persistence;
using ClipGen.Infrastructure.Data;
using ClipGen.Infrastructure.Data.Repositories;
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

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IClipRepository, ClipRepository>();
        services.AddScoped<ITagRepository, TagRepository>();

        return services;
    }
}
