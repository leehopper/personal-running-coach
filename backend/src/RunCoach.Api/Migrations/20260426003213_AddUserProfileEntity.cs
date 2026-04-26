using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunCoach.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserProfile",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryGoal = table.Column<int>(type: "integer", nullable: true),
                    OnboardingCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CurrentFitness = table.Column<string>(type: "jsonb", nullable: true),
                    InjuryHistory = table.Column<string>(type: "jsonb", nullable: true),
                    Preferences = table.Column<string>(type: "jsonb", nullable: true),
                    TargetEvent = table.Column<string>(type: "jsonb", nullable: true),
                    WeeklySchedule = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfile", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserProfile_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserProfile");
        }
    }
}
