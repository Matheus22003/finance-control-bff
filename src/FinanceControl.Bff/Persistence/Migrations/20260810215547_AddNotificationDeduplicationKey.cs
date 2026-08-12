using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceControl.Bff.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDeduplicationKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeduplicationKey",
                table: "notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_DeduplicationKey",
                table: "notifications",
                columns: new[] { "UserId", "DeduplicationKey" },
                unique: true,
                filter: "\"DeduplicationKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_UserId_DeduplicationKey",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "DeduplicationKey",
                table: "notifications");
        }
    }
}
