using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDeskLite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssigneeToTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Assignee",
                table: "Tickets",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Assignee",
                table: "Tickets");
        }
    }
}
