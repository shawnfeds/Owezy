using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Owezy.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBillFinalizedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FinalizedAt",
                table: "Bills",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "Bills");
        }
    }
}
