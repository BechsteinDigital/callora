using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Callora.Core.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBackgroundJobs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "background_jobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                WorkspaceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                ScheduledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_background_jobs", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_background_jobs_JobType",
            table: "background_jobs",
            column: "JobType");

        migrationBuilder.CreateIndex(
            name: "IX_background_jobs_Status_ScheduledAtUtc",
            table: "background_jobs",
            columns: new[] { "Status", "ScheduledAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "background_jobs");
    }
}
