using ErpAssistant.Application.Abstractions;
using ErpAssistant.Infrastructure.Identity;
using ErpAssistant.Infrastructure.Persistence;
using ErpAssistant.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace ErpAssistant.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Veritabanı, kimlik altyapısı ve ortak servisler. Api, Worker ve Mcp hepsi çağırır.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Erp")
            ?? throw new InvalidOperationException("ConnectionStrings:Erp tanımlı değil.");

        services.AddDbContext<ErpDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.EnableRetryOnFailure(5)));
        services.AddScoped<IErpDbContext>(sp => sp.GetRequiredService<ErpDbContext>());

        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUser, HttpContextCurrentUser>();

        services.TryAddSingleton(TimeProvider.System);
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.AddScoped<ErpDataSeeder>();

        services
            .AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 8;
                o.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ErpDbContext>();
        services.AddScoped<IdentitySeeder>();

        return services;
    }

    /// <summary>JWT bearer doğrulaması ve token üretimi (yalnızca Api).</summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(section);
        var jwt = section.Get<JwtOptions>() ?? new JwtOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = TokenService.SigningKey(jwt),
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "email",
                    RoleClaimType = ErpClaims.Role,
                };
                // SSE (EventSource) başlık gönderemez; sohbet akışı için token sorgu parametresinden de okunur.
                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        if (ctx.Request.Path.StartsWithSegments("/api/v1/chat") &&
                            ctx.Request.Query.TryGetValue("access_token", out var token))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddScoped<TokenService>();
        services.AddScoped<PersonalAccessTokenService>();
        return services;
    }
}
