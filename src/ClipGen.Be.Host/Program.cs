using System.Security.Claims;
using System.Text;
using ClipGen.Be.Host.Auth;
using ClipGen.Be.Host.Errors;
using ClipGen.Be.Host.Filters;
using ClipGen.Be.Host.Services;
using ClipGen.Core.Domain;
using ClipGen.Infrastructure;
using ClipGen.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ServiceExceptionHandler>();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ClipGen.Be — Jobs/Clips/Tags",
        Version = "v1",
        Description = """
            Проверка через Swagger:
            1. POST /api/auth/register — создать пользователя.
            2. POST /api/auth/login — скопировать "token" из ответа.
            3. Кнопка Authorize (замок справа сверху) — вставить токен (без префикса "Bearer").
            4. Дальше все запросы уходят с этим токеном автоматически.
            /api/admin/jobs-summary доступен только пользователю с ролью Admin.
            """,
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT, полученный через POST /api/auth/login.",
    });

    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", doc), [] },
    });
});

builder.Services.AddClipGenInfrastructure(builder.Configuration);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<TokenService>();

builder.Services
    .AddIdentityCore<AppUser>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name,
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", p => p.RequireRole("Admin"));

builder.Services.AddScoped<JobOwnershipFilter>();
builder.Services.AddScoped<ClipOwnershipFilter>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<ClipService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<AdminReportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok()).AllowAnonymous();

await SeedIdentityAsync(app);

app.Run();

static async Task SeedIdentityAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    foreach (var role in new[] { "Admin", "User" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
    }

    if (!app.Environment.IsDevelopment())
        return;

    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var adminUserName = config["Seed:AdminUserName"];
    var adminPassword = config["Seed:AdminPassword"];
    if (string.IsNullOrWhiteSpace(adminUserName) || string.IsNullOrWhiteSpace(adminPassword))
        return;

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var admin = await userManager.FindByNameAsync(adminUserName);
    if (admin is null)
    {
        admin = new AppUser { UserName = adminUserName };
        var result = await userManager.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
            return;
    }

    if (!await userManager.IsInRoleAsync(admin, "Admin"))
        await userManager.AddToRoleAsync(admin, "Admin");
}

public partial class Program;
