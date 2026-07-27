using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicSignal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentMediaAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "analysis_confidence",
                table: "incident_media",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "analysis_error",
                table: "incident_media",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "analysis_model_name",
                table: "incident_media",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "analysis_model_version",
                table: "incident_media",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "analysis_processing_time_milliseconds",
                table: "incident_media",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "analysis_status",
                table: "incident_media",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "analysis_summary",
                table: "incident_media",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "analyzed_at",
                table: "incident_media",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "detected_labels",
                table: "incident_media",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transcript",
                table: "incident_media",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "analysis_confidence",
                table: "incident_media");

            migrationBuilder.DropColumn(
                name: "analysis_error",
                table: "incident_media");

            migrationBuilder.DropColumn(
                name: "analysis_model_name",
                table: "incident_media");

            migrationBuilder.DropColumn(
                name: "analysis_model_version",
                table: "incident_media");

            migrationBuilder.DropColumn(
                name: "analysis_processing_time_milliseconds",
                table: "incident_media");

            migrationBuilder.DropColumn(
                name: "analysis_status",
                table: "incident_media");

            migrationBuilder.DropColumn(
                name: "analysis_summary",
                table: "incident_media");

            migrationBuilder.DropColumn(
                name: "analyzed_at",
                table: "incident_media");

            migrationBuilder.DropColumn(
                name: "detected_labels",
                table: "incident_media");

            migrationBuilder.DropColumn(
                name: "transcript",
                table: "incident_media");
        }
    }
}
