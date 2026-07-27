using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace CivicSignal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalComplaints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "historical_complaints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    external_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    complaint_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    descriptor = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    agency = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    agency_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    borough = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    incident_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    resolution_description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<Point>(type: "geography(point,4326)", nullable: false, computedColumnSql: "ST_SetSRID(ST_MakePoint(longitude, latitude), 4326)::geography", stored: true),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historical_complaints", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_historical_complaints_agency",
                table: "historical_complaints",
                column: "agency");

            migrationBuilder.CreateIndex(
                name: "ix_historical_complaints_borough",
                table: "historical_complaints",
                column: "borough");

            migrationBuilder.CreateIndex(
                name: "ix_historical_complaints_category",
                table: "historical_complaints",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_historical_complaints_complaint_type",
                table: "historical_complaints",
                column: "complaint_type");

            migrationBuilder.CreateIndex(
                name: "ix_historical_complaints_created_at",
                table: "historical_complaints",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_historical_complaints_location",
                table: "historical_complaints",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_historical_complaints_status",
                table: "historical_complaints",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_historical_complaints_source_external_id",
                table: "historical_complaints",
                columns: new[] { "source", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "historical_complaints");
        }
    }
}
