using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KastomniMebli.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class B2b : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "due_date",
                table: "orders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "telegram",
                table: "clients",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_kind",
                table: "orders",
                column: "kind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_kind",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "due_date",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "telegram",
                table: "clients");
        }
    }
}
