using Hangfire;
using Hangfire.SqlServer;
using LeadDelivery.Worker;
using LeadDelivery.Infrastructure;
using LeadDelivery.Infrastructure.Persistence;
using LeadDelivery.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole();

builder.Services.AddLeadDeliveryInfrastructure(builder.Configuration);

builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("LeadDelivery")));

builder.Services.AddHangfireServer();

builder.Services.AddScoped<OutboxJobRunner>();
builder.Services.AddHostedService<OutboxBackgroundService>();
builder.Services.AddHostedService<LeadSourcePoller>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LeadDeliveryDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();

public sealed class OutboxBackgroundService(IRecurringJobManager recurringJobs) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        recurringJobs.AddOrUpdate<OutboxJobRunner>("outbox-process", j => j.RunAsync(), Cron.Minutely);
        return Task.CompletedTask;
    }
}

public sealed class OutboxJobRunner(OutboxProcessor processor)
{
    public Task RunAsync() => processor.ProcessPendingAsync(CancellationToken.None);
}