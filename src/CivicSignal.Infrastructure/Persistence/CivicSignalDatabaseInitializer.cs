using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CivicSignal.Infrastructure.Persistence;

public static class CivicSignalDatabaseInitializer
{
    public static async Task ApplyDatabaseMigrationsAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var options = configuration
            .GetSection(DatabaseStartupOptions.SectionName)
            .Get<DatabaseStartupOptions>() ?? new DatabaseStartupOptions();

        if (!options.MigrateOnStartup)
        {
            return;
        }

        using var scope = services.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        if (options.DevelopmentOnly && !environment.IsDevelopment())
        {
            logger.LogInformation("Skipping database migrations outside Development environment.");
            return;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<CivicSignalDbContext>();

        logger.LogInformation("Applying CivicSignal database migrations.");
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
