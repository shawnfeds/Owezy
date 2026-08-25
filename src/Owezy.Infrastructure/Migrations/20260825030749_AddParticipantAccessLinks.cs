using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Owezy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantAccessLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParticipantAccessLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantAccessLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipantAccessLinks_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantAccessLinks_BillId",
                table: "ParticipantAccessLinks",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantAccessLinks_TokenHash",
                table: "ParticipantAccessLinks",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParticipantAccessLinks");
        }
    }
}
