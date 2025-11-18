using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStatusEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
               name: "BlockedAt",
               table: "Users",
               type: "timestamp with time zone",
               nullable: true);

            migrationBuilder.AddColumn<string>(
               name: "Status",
               table: "Users",
               type: "text",
               nullable: false,
               defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
               name: "BlockedAt",
               table: "Users");

            migrationBuilder.DropColumn(
               name: "Status",
               table: "Users");
        }
    }
}
