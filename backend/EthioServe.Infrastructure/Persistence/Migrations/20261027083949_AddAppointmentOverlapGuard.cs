using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EthioServe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentOverlapGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A provider cannot hold two overlapping active appointments. The database enforces it, so two requests that
            // arrive at the same moment cannot both succeed. Cancelled, completed and missed appointments do not count.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql("ALTER TABLE appointments ADD CONSTRAINT ex_appointments_provider_no_overlap EXCLUDE USING gist (provider_id WITH =, tstzrange(start_at, end_at) WITH &&) WHERE (status IN ('Scheduled', 'Confirmed', 'InProgress'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE appointments DROP CONSTRAINT IF EXISTS ex_appointments_provider_no_overlap;");
        }
    }
}
