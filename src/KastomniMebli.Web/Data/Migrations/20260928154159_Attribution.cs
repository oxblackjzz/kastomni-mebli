using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KastomniMebli.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Attribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "campaign",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "channel",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "referrer",
                table: "leads",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "utm_campaign",
                table: "leads",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "utm_medium",
                table: "leads",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "utm_source",
                table: "leads",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "campaign",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "channel",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "referrer",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "utm_campaign",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "utm_medium",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "utm_source",
                table: "leads");
        }
    }
}
