using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KastomniMebli.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Portfolio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "portfolio_works",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    on_home = table.Column<bool>(type: "boolean", nullable: false),
                    for_makers = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolio_works", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "portfolio_photos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    work_id = table.Column<int>(type: "integer", nullable: false),
                    large_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    thumb_path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    caption = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolio_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_portfolio_photos_portfolio_works_work_id",
                        column: x => x.work_id,
                        principalTable: "portfolio_works",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_photos_work_id",
                table: "portfolio_photos",
                column: "work_id");

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_works_is_published_sort_order",
                table: "portfolio_works",
                columns: new[] { "is_published", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portfolio_photos");

            migrationBuilder.DropTable(
                name: "portfolio_works");
        }
    }
}
