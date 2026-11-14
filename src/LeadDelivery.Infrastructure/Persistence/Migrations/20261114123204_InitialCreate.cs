using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadDelivery.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllowedStoredProcedures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchemaName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProcedureName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowedStoredProcedures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CredentialProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CredentialProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnrichmentSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StepType = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrichmentSteps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FieldCatalogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    IsPii = table.Column<bool>(type: "bit", nullable: false),
                    DeprecatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ParentKey = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldCatalogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FieldCatalogInferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuggestedKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SampleValueRedacted = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    IsIgnored = table.Column<bool>(type: "bit", nullable: false),
                    DiscoveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldCatalogInferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Institutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApiKeyHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ValueLookupTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MappingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValueLookupTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    PublishState = table.Column<int>(type: "int", nullable: false),
                    RulesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContinueOnPrimaryFailure = table.Column<bool>(type: "bit", nullable: false),
                    EnrichmentPipelineJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryConfigurations_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeadDocumentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Campus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Program = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceSite = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalSourceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryEndpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChannelType = table.Column<int>(type: "int", nullable: false),
                    ChannelConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    FieldMappingsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BodyTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryEndpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryEndpoints_DeliveryConfigurations_DeliveryConfigurationId",
                        column: x => x.DeliveryConfigurationId,
                        principalTable: "DeliveryConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveryConfigurationVersion = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EnrichedLeadSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryRuns_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryEndpointId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndpointName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChannelType = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    RequestSnapshotRedacted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseSnapshotRedacted = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryAttempts_DeliveryRuns_DeliveryRunId",
                        column: x => x.DeliveryRunId,
                        principalTable: "DeliveryRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAttempts_DeliveryRunId",
                table: "DeliveryAttempts",
                column: "DeliveryRunId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryConfigurations_InstitutionId",
                table: "DeliveryConfigurations",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryEndpoints_DeliveryConfigurationId",
                table: "DeliveryEndpoints",
                column: "DeliveryConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRuns_LeadId",
                table: "DeliveryRuns",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldCatalogEntries_InstitutionId_Key",
                table: "FieldCatalogEntries",
                columns: new[] { "InstitutionId", "Key" },
                unique: true,
                filter: "[InstitutionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Institutions_Code",
                table: "Institutions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_InstitutionId",
                table: "Leads",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_CreatedAt",
                table: "OutboxMessages",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllowedStoredProcedures");

            migrationBuilder.DropTable(
                name: "CredentialProfiles");

            migrationBuilder.DropTable(
                name: "DeliveryAttempts");

            migrationBuilder.DropTable(
                name: "DeliveryEndpoints");

            migrationBuilder.DropTable(
                name: "EnrichmentSteps");

            migrationBuilder.DropTable(
                name: "FieldCatalogEntries");

            migrationBuilder.DropTable(
                name: "FieldCatalogInferences");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "ValueLookupTables");

            migrationBuilder.DropTable(
                name: "DeliveryRuns");

            migrationBuilder.DropTable(
                name: "DeliveryConfigurations");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "Institutions");
        }
    }
}
