using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDeskLite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketRefSuffix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefSuffix",
                table: "Tickets",
                type: "text",
                nullable: true,
                computedColumnSql: "right(\"Id\"::text, 6)",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RefSuffix",
                table: "Tickets",
                column: "RefSuffix");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_RefSuffix",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "RefSuffix",
                table: "Tickets");
        }
    }
}
