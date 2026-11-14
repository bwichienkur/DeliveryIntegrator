using System.Text.Json;
using LeadDelivery.Domain.Entities;
using LeadDelivery.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LeadDelivery.Infrastructure.Persistence;

public static class SeedData
{
    public static async Task InitializeAsync(LeadDeliveryDbContext db, CancellationToken ct = default)
    {
        if (await db.Institutions.AnyAsync(ct)) return;

        var inst1 = new Institution { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Code = "CONTOSOU", Name = "Contoso University" };
        var inst2 = new Institution { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Code = "FABRIKAM", Name = "Fabrikam College" };
        db.Institutions.AddRange(inst1, inst2);

        db.AllowedStoredProcedures.Add(new AllowedStoredProcedure
        {
            Id = Guid.NewGuid(),
            ProcedureName = "ld_GetProgramDetails",
            SchemaName = "ld"
        });

        var config = new DeliveryConfiguration
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            InstitutionId = inst1.Id,
            Name = "Contoso Online MBA",
            Priority = 1,
            Version = 1,
            PublishState = ConfigurationPublishState.Published,
            PublishedAt = DateTimeOffset.UtcNow,
            ContinueOnPrimaryFailure = false,
            RulesJson = """[{"field":"campus","operator":"equals","value":"Online"},{"field":"program","operator":"equals","value":"MBA"}]"""
        };

        config.Endpoints.Add(new DeliveryEndpoint
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Name = "CRM Primary",
            ChannelType = ChannelType.Http,
            Priority = EndpointPriority.Primary,
            SortOrder = 1,
            ChannelConfigJson = JsonSerializer.Serialize(new
            {
                url = "http://wiremock:8080/crm",
                method = "POST",
                contentType = "application/json",
                successStatusCodes = new[] { 200, 201 },
                bodyTemplate = "{value lead}"
            })
        });

        config.Endpoints.Add(new DeliveryEndpoint
        {
            Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Name = "Legacy XML",
            ChannelType = ChannelType.Http,
            Priority = EndpointPriority.Secondary,
            SortOrder = 2,
            ChannelConfigJson = JsonSerializer.Serialize(new
            {
                url = "http://wiremock:8080/legacy",
                method = "POST",
                contentType = "application/xml",
                successStatusCodes = new[] { 200 },
                bodyTemplate = "{value lead}"
            })
        });

        db.DeliveryConfigurations.Add(config);

        db.EnrichmentSteps.Add(new EnrichmentStepDefinition
        {
            Id = Guid.NewGuid(),
            InstitutionId = inst1.Id,
            StepKey = "ld_GetProgramDetails",
            StepType = EnrichmentStepType.StoredProcedure,
            SortOrder = 1,
            ConfigJson = JsonSerializer.Serialize(new
            {
                procedureName = "ld_GetProgramDetails",
                parameters = new[] { new { parameterName = "@Program", sourcePath = "lead.program" } }
            })
        });

        db.FieldCatalogEntries.AddRange(
            new FieldCatalogEntry { Id = Guid.NewGuid(), InstitutionId = inst1.Id, Key = "lead.email", DisplayName = "Email", DataType = "string", Path = "lead.email", Source = FieldCatalogSource.Column, IsPii = true },
            new FieldCatalogEntry { Id = Guid.NewGuid(), InstitutionId = inst1.Id, Key = "lead.campus", DisplayName = "Campus", DataType = "string", Path = "lead.campus", Source = FieldCatalogSource.Column },
            new FieldCatalogEntry { Id = Guid.NewGuid(), InstitutionId = inst1.Id, Key = "lead.program", DisplayName = "Program", DataType = "string", Path = "lead.program", Source = FieldCatalogSource.Column }
        );

        await db.SaveChangesAsync(ct);
    }
}