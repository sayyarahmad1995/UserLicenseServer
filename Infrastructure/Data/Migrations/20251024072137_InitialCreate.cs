using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
               name: "Users",
               columns: table => new
               {
                   Id = table.Column<int>(type: "integer", nullable: false)
                     .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                   Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                   Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                   PasswordHash = table.Column<string>(type: "text", nullable: false),
                   Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                   CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                   VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                   UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                   LastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
               },
               constraints: table =>
               {
                   table.PrimaryKey("PK_Users", x => x.Id);
               });

            migrationBuilder.CreateTable(
               name: "Licenses",
               columns: table => new
               {
                   Id = table.Column<int>(type: "integer", nullable: false)
                     .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                   LicenseKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                   CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                   ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                   RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                   Status = table.Column<string>(type: "text", nullable: false),
                   UserId = table.Column<int>(type: "integer", nullable: false)
               },
               constraints: table =>
               {
                   table.PrimaryKey("PK_Licenses", x => x.Id);
                   table.ForeignKey(
                   name: "FK_Licenses_Users_UserId",
                   column: x => x.UserId,
                   principalTable: "Users",
                   principalColumn: "Id",
                   onDelete: ReferentialAction.Cascade);
               });

            migrationBuilder.CreateIndex(
               name: "IX_Licenses_UserId_Status_Active",
               table: "Licenses",
               columns: new[] { "UserId", "Status" },
               unique: true,
               filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
               name: "IX_Licenses_UserId_Status_ExpiresAt",
               table: "Licenses",
               columns: new[] { "UserId", "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
               name: "Licenses");

            migrationBuilder.DropTable(
               name: "Users");
        }
    }
}
