using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EthioServe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NameConstraintIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_appointments_service_request_id",
                table: "appointments");

            migrationBuilder.RenameIndex(
                name: "ix_quotes_service_request_id1",
                table: "quotes",
                newName: "ux_quotes_one_accepted_per_request");

            migrationBuilder.RenameIndex(
                name: "ix_quotes_service_request_id_provider_id",
                table: "quotes",
                newName: "ux_quotes_one_pending_per_provider");

            migrationBuilder.RenameIndex(
                name: "ix_payments_service_request_id1",
                table: "payments",
                newName: "ux_payments_one_completed_per_request");

            migrationBuilder.RenameIndex(
                name: "ix_appointments_service_request_id1",
                table: "appointments",
                newName: "ux_appointments_one_active_per_request");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "ux_quotes_one_pending_per_provider",
                table: "quotes",
                newName: "ix_quotes_service_request_id_provider_id");

            migrationBuilder.RenameIndex(
                name: "ux_quotes_one_accepted_per_request",
                table: "quotes",
                newName: "ix_quotes_service_request_id1");

            migrationBuilder.RenameIndex(
                name: "ux_payments_one_completed_per_request",
                table: "payments",
                newName: "ix_payments_service_request_id1");

            migrationBuilder.RenameIndex(
                name: "ux_appointments_one_active_per_request",
                table: "appointments",
                newName: "ix_appointments_service_request_id1");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_service_request_id",
                table: "appointments",
                column: "service_request_id");
        }
    }
}
