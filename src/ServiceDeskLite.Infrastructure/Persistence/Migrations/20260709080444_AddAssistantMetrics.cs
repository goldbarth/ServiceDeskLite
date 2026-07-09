using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDeskLite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssistantTokenUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantTokenUsages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssistantToolInvocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsError = table.Column<bool>(type: "boolean", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    MatchCount = table.Column<int>(type: "integer", nullable: true),
                    SemanticAvailable = table.Column<bool>(type: "boolean", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantToolInvocations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantTokenUsages_OccurredAt",
                table: "AssistantTokenUsages",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantToolInvocations_OccurredAt",
                table: "AssistantToolInvocations",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantTokenUsages");

            migrationBuilder.DropTable(
                name: "AssistantToolInvocations");
        }
    }
}
