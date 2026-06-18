using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class CEAddMetaChestItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_meta_chest_item",
                columns: table => new
                {
                    player_meta_chest_item_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chest_slot = table.Column<int>(type: "integer", nullable: false),
                    item_yaml = table.Column<string>(type: "text", nullable: false),
                    grid_x = table.Column<int>(type: "integer", nullable: false),
                    grid_y = table.Column<int>(type: "integer", nullable: false),
                    grid_rotation = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_meta_chest_item", x => x.player_meta_chest_item_id);
                    table.ForeignKey(
                        name: "FK_player_meta_chest_item_player_player_user_id",
                        column: x => x.player_user_id,
                        principalTable: "player",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_meta_chest_item_player_user_id_chest_slot",
                table: "player_meta_chest_item",
                columns: new[] { "player_user_id", "chest_slot" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_meta_chest_item");
        }
    }
}
