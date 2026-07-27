using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicSignal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentReviewCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "accepted_prediction",
                table: "incidents",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "corrected_agency_code",
                table: "incidents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "corrected_category",
                table: "incidents",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "corrected_severity",
                table: "incidents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "duplicate_of_incident_id",
                table: "incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "incident_review_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reviewer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    corrected_category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    corrected_agency_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    corrected_severity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    duplicate_of_incident_id = table.Column<Guid>(type: "uuid", nullable: true),
                    accepted_prediction = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_review_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_incident_review_records_incidents_incident_id",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_incident_review_records_incident_id_created_at",
                table: "incident_review_records",
                columns: new[] { "incident_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_review_records");

            migrationBuilder.DropColumn(
                name: "accepted_prediction",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "corrected_agency_code",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "corrected_category",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "corrected_severity",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "duplicate_of_incident_id",
                table: "incidents");
        }
    }
}
