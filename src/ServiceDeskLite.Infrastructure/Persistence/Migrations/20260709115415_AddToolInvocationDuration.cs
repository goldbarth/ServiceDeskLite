using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDeskLite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddToolInvocationDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DurationMs",
                table: "AssistantToolInvocations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMs",
                table: "AssistantToolInvocations");
        }
    }
}
