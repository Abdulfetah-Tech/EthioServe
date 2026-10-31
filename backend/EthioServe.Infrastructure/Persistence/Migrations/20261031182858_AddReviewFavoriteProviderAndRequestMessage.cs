using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EthioServe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewFavoriteProviderAndRequestMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications");

            migrationBuilder.CreateTable(
                name: "favorite_providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_favorite_providers", x => x.id);
                    table.ForeignKey(
                        name: "fk_favorite_providers_customer_profiles_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_favorite_providers_provider_profiles_provider_id",
                        column: x => x.provider_id,
                        principalTable: "provider_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "request_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_request_messages_provider_profiles_provider_id",
                        column: x => x.provider_id,
                        principalTable: "provider_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_request_messages_service_requests_service_request_id",
                        column: x => x.service_request_id,
                        principalTable: "service_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_request_messages_users_sender_user_id",
                        column: x => x.sender_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications",
                sql: "type IN ('General', 'NewServiceRequest', 'QuoteReceived', 'QuoteAccepted', 'AppointmentUpdated', 'PaymentCompleted', 'ReviewReceived', 'ProviderVerified', 'NewMessage')");

            migrationBuilder.CreateIndex(
                name: "ix_favorite_providers_provider_id",
                table: "favorite_providers",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "ux_favorite_providers_customer_provider",
                table: "favorite_providers",
                columns: new[] { "customer_id", "provider_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_request_messages_provider_id",
                table: "request_messages",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_messages_request_provider_created",
                table: "request_messages",
                columns: new[] { "service_request_id", "provider_id", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_request_messages_sender_user_id",
                table: "request_messages",
                column: "sender_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "favorite_providers");

            migrationBuilder.DropTable(
                name: "request_messages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_type",
                table: "notifications",
                sql: "type IN ('General', 'NewServiceRequest', 'QuoteReceived', 'QuoteAccepted', 'AppointmentUpdated', 'PaymentCompleted', 'ReviewReceived', 'ProviderVerified')");
        }
    }
}
