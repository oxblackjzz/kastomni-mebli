using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KastomniMebli.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Posts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "posts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    text = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_draft = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_posts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "post_photos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    post_id = table.Column<int>(type: "integer", nullable: false),
                    path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    public_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_post_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_post_photos_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "post_targets",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    post_id = table.Column<int>(type: "integer", nullable: false),
                    network = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    external_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_post_targets", x => x.id);
                    table.ForeignKey(
                        name: "fk_post_targets_posts_post_id",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_post_photos_post_id",
                table: "post_photos",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "ix_post_photos_public_key",
                table: "post_photos",
                column: "public_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_post_targets_post_id_network",
                table: "post_targets",
                columns: new[] { "post_id", "network" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_post_targets_status",
                table: "post_targets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_posts_scheduled_at",
                table: "posts",
                column: "scheduled_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "post_photos");

            migrationBuilder.DropTable(
                name: "post_targets");

            migrationBuilder.DropTable(
                name: "posts");
        }
    }
}
