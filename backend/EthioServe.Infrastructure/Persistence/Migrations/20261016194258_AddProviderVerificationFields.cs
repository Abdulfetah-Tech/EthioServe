using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EthioServe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderVerificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "verification_note",
                table: "provider_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "verification_reviewed_at",
                table: "provider_profiles",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "verification_note",
                table: "provider_profiles");

            migrationBuilder.DropColumn(
                name: "verification_reviewed_at",
                table: "provider_profiles");
        }
    }
}
