using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunCoach.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkoutLog",
                schema: "public",
                columns: table => new
                {
                    WorkoutLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Distance = table.Column<double>(type: "double precision", nullable: false),
                    Duration = table.Column<long>(type: "bigint", nullable: false),
                    CompletionStatus = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Metrics = table.Column<string>(type: "jsonb", nullable: true),
                    Splits = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Prescription_DayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    Prescription_PrescribedDistance = table.Column<double>(type: "double precision", nullable: true),
                    Prescription_PrescribedDuration = table.Column<long>(type: "bigint", nullable: true),
                    Prescription_PrescribedPaceFast = table.Column<double>(type: "double precision", nullable: true),
                    Prescription_PrescribedPaceSlow = table.Column<double>(type: "double precision", nullable: true),
                    Prescription_SourcePlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    Prescription_WeekNumber = table.Column<int>(type: "integer", nullable: true),
                    Prescription_WorkoutType = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutLog", x => x.WorkoutLogId);
                    table.ForeignKey(
                        name: "FK_WorkoutLog_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "public",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLog_UserId_OccurredOn_WorkoutLogId",
                schema: "public",
                table: "WorkoutLog",
                columns: new[] { "UserId", "OccurredOn", "WorkoutLogId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkoutLog",
                schema: "public");
        }
    }
}
