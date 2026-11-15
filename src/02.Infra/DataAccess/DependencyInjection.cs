using ChangeFlow.Infra.DataAccess.Common;
using ChangeFlow.Core.Application.Users.CreateUser;
using ChangeFlow.Infra.DataAccess.Users.Persistence;
using ChangeFlow.Infra.DataAccess.Users.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeFlow.Infra.DataAccess;

public static class DependencyInjection
{
    private const string DefaultConnectionString =
        "Data Source=changeflow.db;Foreign Keys=True;Default Timeout=5";

    public static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ChangeFlow")
            ?? DefaultConnectionString;

        services.AddDbContextFactory<ChangeFlowDbContext>(options =>
            options.UseSqlite(connectionString));
        services.AddSingleton<IPasswordCredentialHasher, IdentityPasswordCredentialHasher>();
        services.AddSingleton<ISetupCodeValidator, ConfigurationSetupCodeValidator>();
        services.AddScoped<IUserProvisioningStore, EfUserProvisioningStore>();

        return services;
    }
}
