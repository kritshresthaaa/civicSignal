using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicSignal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPredictionEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "model_version",
                table: "triage_predictions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "processing_time_ms",
                table: "triage_predictions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prompt_version",
                table: "triage_predictions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "prediction_evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    triage_prediction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prediction_evidence", x => x.id);
                    table.ForeignKey(
                        name: "FK_prediction_evidence_triage_predictions_triage_prediction_id",
                        column: x => x.triage_prediction_id,
                        principalTable: "triage_predictions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_prediction_evidence_triage_prediction_id_created_at",
                table: "prediction_evidence",
                columns: new[] { "triage_prediction_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prediction_evidence");

            migrationBuilder.DropColumn(
                name: "model_version",
                table: "triage_predictions");

            migrationBuilder.DropColumn(
                name: "processing_time_ms",
                table: "triage_predictions");

            migrationBuilder.DropColumn(
                name: "prompt_version",
                table: "triage_predictions");
        }
    }
}
