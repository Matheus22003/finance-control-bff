using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceControl.Bff.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileSessionDeviceBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceInstallationId",
                table: "user_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceInstallationId",
                table: "user_sessions");
        }
    }
}
