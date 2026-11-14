using LeadDelivery.Application.Abstractions;
using LeadDelivery.Infrastructure.Channels;
using LeadDelivery.Infrastructure.Persistence;
using LeadDelivery.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace LeadDelivery.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLeadDeliveryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LeadDeliveryDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("LeadDelivery")));

        services.AddScoped<LeadDocumentService>();
        services.AddScoped<FieldCatalogService>();
        services.AddScoped<RuleMatchingService>();
        services.AddScoped<EnrichmentPipelineService>();
        services.AddScoped<DeliveryOrchestrator>();
        services.AddScoped<LeadIngestionService>();
        services.AddScoped<OutboxProcessor>();

        services.AddScoped<HttpDeliveryChannelExecutor>();
        services.AddScoped<EmailDeliveryChannelExecutor>();
        services.AddScoped<SftpDeliveryChannelExecutor>();
        services.AddScoped<IDeliveryChannelExecutor>(sp => sp.GetRequiredService<HttpDeliveryChannelExecutor>());
        services.AddScoped<IDeliveryChannelExecutor>(sp => sp.GetRequiredService<EmailDeliveryChannelExecutor>());
        services.AddScoped<IDeliveryChannelExecutor>(sp => sp.GetRequiredService<SftpDeliveryChannelExecutor>());

        services.AddHttpClient("delivery")
            .AddPolicyHandler(HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(2, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));

        return services;
    }
}