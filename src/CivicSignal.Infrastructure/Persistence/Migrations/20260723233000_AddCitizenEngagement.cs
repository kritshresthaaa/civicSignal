using System;
using CivicSignal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicSignal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CivicSignalDbContext))]
    [Migration("20260723233000_AddCitizenEngagement")]
    public partial class AddCitizenEngagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "notification_alerts_enabled",
                table: "incidents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "notification_channel",
                table: "incidents",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "notification_preference_updated_at",
                table: "incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "incident_feedback",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_feedback", x => x.id);
                    table.CheckConstraint("ck_incident_feedback_rating_range", "rating >= 1 AND rating <= 5");
                    table.ForeignKey(
                        name: "FK_incident_feedback_incidents_incident_id",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "incident_update_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_update_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_incident_update_requests_incidents_incident_id",
                        column: x => x.incident_id,
                        principalTable: "incidents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_incident_feedback_incident_id_created_at",
                table: "incident_feedback",
                columns: new[] { "incident_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_incident_update_requests_incident_id_created_at",
                table: "incident_update_requests",
                columns: new[] { "incident_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_feedback");

            migrationBuilder.DropTable(
                name: "incident_update_requests");

            migrationBuilder.DropColumn(
                name: "notification_alerts_enabled",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "notification_channel",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "notification_preference_updated_at",
                table: "incidents");
        }
    }
}
