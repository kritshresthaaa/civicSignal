using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicSignal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDataImportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_import_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    import_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    parameters_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    received_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    skipped_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_import_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_data_import_jobs_requested_at",
                table: "data_import_jobs",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "ix_data_import_jobs_source",
                table: "data_import_jobs",
                column: "source");

            migrationBuilder.CreateIndex(
                name: "ix_data_import_jobs_source_status_requested_at",
                table: "data_import_jobs",
                columns: new[] { "source", "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_data_import_jobs_status",
                table: "data_import_jobs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_import_jobs");
        }
    }
}
