using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DocumentProcessing.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorrectionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrectedValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExtractionTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PromptTemplate = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Source = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_ExtractionTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "ExtractionTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TemplateField",
                columns: table => new
                {
                    ExtractionTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateField", x => new { x.ExtractionTemplateId, x.Id });
                    table.ForeignKey(
                        name: "FK_TemplateField_ExtractionTemplates_ExtractionTemplateId",
                        column: x => x.ExtractionTemplateId,
                        principalTable: "ExtractionTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessingResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    RawOcrText = table.Column<string>(type: "text", nullable: true),
                    AiProvider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RawAiResponse = table.Column<string>(type: "text", nullable: true),
                    Metrics_OcrDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Metrics_AiDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Metrics_TotalDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Metrics_TokenCount = table.Column<int>(type: "integer", nullable: false),
                    Metrics_EstimatedCost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessingResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessingResults_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProcessingResultId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentFields_ProcessingResults_ProcessingResultId",
                        column: x => x.ProcessingResultId,
                        principalTable: "ProcessingResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorrectionLogs_DocumentId",
                table: "CorrectionLogs",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFields_ProcessingResultId",
                table: "DocumentFields",
                column: "ProcessingResultId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Status",
                table: "Documents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_TemplateId",
                table: "Documents",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedAt",
                table: "Documents",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionTemplates_Name",
                table: "ExtractionTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessingResults_DocumentId",
                table: "ProcessingResults",
                column: "DocumentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrectionLogs");

            migrationBuilder.DropTable(
                name: "DocumentFields");

            migrationBuilder.DropTable(
                name: "TemplateField");

            migrationBuilder.DropTable(
                name: "ProcessingResults");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "ExtractionTemplates");
        }
    }
}
