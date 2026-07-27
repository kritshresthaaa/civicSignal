using CivicSignal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicSignal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CivicSignalDbContext))]
    [Migration("20260724120000_AddIncidentPublicTrackingCodes")]
    public partial class AddIncidentPublicTrackingCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "public_tracking_code",
                table: "incidents",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH constants AS (
                    SELECT 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789' AS alphabet
                ),
                numbered AS (
                    SELECT
                        id,
                        row_number() OVER (ORDER BY created_at, id)::bigint AS n
                    FROM incidents
                )
                UPDATE incidents AS incident
                SET public_tracking_code =
                    'CS-' ||
                    substr(constants.alphabet, ((numbered.n / 34359738368) % 32)::int + 1, 1) ||
                    substr(constants.alphabet, ((numbered.n / 1073741824) % 32)::int + 1, 1) ||
                    substr(constants.alphabet, ((numbered.n / 33554432) % 32)::int + 1, 1) ||
                    substr(constants.alphabet, ((numbered.n / 1048576) % 32)::int + 1, 1) ||
                    '-' ||
                    substr(constants.alphabet, ((numbered.n / 32768) % 32)::int + 1, 1) ||
                    substr(constants.alphabet, ((numbered.n / 1024) % 32)::int + 1, 1) ||
                    substr(constants.alphabet, ((numbered.n / 32) % 32)::int + 1, 1) ||
                    substr(constants.alphabet, (numbered.n % 32)::int + 1, 1)
                FROM numbered
                CROSS JOIN constants
                WHERE incident.id = numbered.id;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "public_tracking_code",
                table: "incidents",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_incidents_public_tracking_code",
                table: "incidents",
                column: "public_tracking_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_incidents_public_tracking_code",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "public_tracking_code",
                table: "incidents");
        }
    }
}
