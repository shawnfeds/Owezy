using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Owezy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateReceiptConfirmedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAt",
                table: "Receipts",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "Receipts");
        }
    }
}
