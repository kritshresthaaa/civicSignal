using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicSignal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "review_decision",
                table: "incidents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "review_note",
                table: "incidents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                table: "incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by_user_id",
                table: "incidents",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "review_decision",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "review_note",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "reviewed_by_user_id",
                table: "incidents");
        }
    }
}
