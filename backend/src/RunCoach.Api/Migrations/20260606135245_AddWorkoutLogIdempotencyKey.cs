using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RunCoach.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutLogIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyKey",
                schema: "public",
                table: "WorkoutLog",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.CreateIndex(
                name: "ix_workoutlog_user_idempotencykey",
                schema: "public",
                table: "WorkoutLog",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workoutlog_user_idempotencykey",
                schema: "public",
                table: "WorkoutLog");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "public",
                table: "WorkoutLog");
        }
    }
}
