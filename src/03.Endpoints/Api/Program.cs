using ChangeFlow.Endpoints.Api.ChangeRequests;
using ChangeFlow.Endpoints.Api.Common.Observability;
using ChangeFlow.Endpoints.Api.Users.CreateUser;
using ChangeFlow.Core.Application.ChangeRequests;
using ChangeFlow.Core.Application.Users.CreateUser;
using ChangeFlow.Infra.DataAccess;
using ChangeFlow.Infra.DataAccess.Common;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddChangeRequestAuthentication();
builder.Services.AddDataAccess(builder.Configuration);
builder.Services.AddScoped<CreateUserUseCase>();
builder.Services.AddScoped<DraftWorkflow>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ReviewWorkflow>();

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapCreateUser();
app.MapChangeRequestEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    var factory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<ChangeFlowDbContext>>();
    await using var context = await factory.CreateDbContextAsync();
    await context.Database.MigrateAsync();
}

app.Run();

public partial class Program;
