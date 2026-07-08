using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ServiceDeskLite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Owner = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessages", x => new { x.ConversationId, x.Sequence });
                });

            migrationBuilder.CreateTable(
                name: "Memories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Owner = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Vector = table.Column<Vector>(type: "vector(1024)", nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_Owner_ConversationId",
                table: "ConversationMessages",
                columns: new[] { "Owner", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Memories_Owner",
                table: "Memories",
                column: "Owner");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationMessages");

            migrationBuilder.DropTable(
                name: "Memories");
        }
    }
}
