using System;
using CivicSignal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicSignal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CivicSignalDbContext))]
    [Migration("20260724190000_AddIncidentStaffOperations")]
    public partial class AddIncidentStaffOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "assigned_agency_code",
                table: "incidents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "assigned_at",
                table: "incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_by_user_id",
                table: "incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "assigned_team",
                table: "incidents",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dispatched_at",
                table: "incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "dispatched_by_user_id",
                table: "incidents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "duplicate_linked_at",
                table: "incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "duplicate_linked_by_user_id",
                table: "incidents",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "assigned_agency_code",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "assigned_at",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "assigned_by_user_id",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "assigned_team",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "dispatched_at",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "dispatched_by_user_id",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "duplicate_linked_at",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "duplicate_linked_by_user_id",
                table: "incidents");
        }
    }
}
