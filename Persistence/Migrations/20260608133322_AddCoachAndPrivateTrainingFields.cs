using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymSaaS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachAndPrivateTrainingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add columns to Coach table
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "core",
                table: "Coaches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoachTarget",
                schema: "core",
                table: "Coaches",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Add foreign key for Coach.UserId
            migrationBuilder.AddForeignKey(
                name: "FK_Coaches_Users_UserId",
                schema: "core",
                table: "Coaches",
                column: "UserId",
                principalSchema: "identityx",
                principalTable: "Users",
                principalColumn: "UserId");

            // Add column to PackageDefinition table
            migrationBuilder.AddColumn<bool>(
                name: "IsPrivateTraining",
                schema: "membership",
                table: "PackageDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Add column to MemberPackage table
            migrationBuilder.AddColumn<Guid>(
                name: "CoachId",
                schema: "membership",
                table: "MemberPackages",
                type: "uniqueidentifier",
                nullable: true);

            // Add foreign key for MemberPackage.CoachId
            migrationBuilder.AddForeignKey(
                name: "FK_MemberPackages_Coaches_CoachId",
                schema: "membership",
                table: "MemberPackages",
                column: "CoachId",
                principalSchema: "core",
                principalTable: "Coaches",
                principalColumn: "CoachId");

            // Add index for better query performance
            migrationBuilder.CreateIndex(
                name: "IX_MemberPackages_CoachId",
                schema: "membership",
                table: "MemberPackages",
                column: "CoachId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign key for MemberPackage.CoachId
            migrationBuilder.DropForeignKey(
                name: "FK_MemberPackages_Coaches_CoachId",
                schema: "membership",
                table: "MemberPackages");

            // Drop foreign key for Coach.UserId
            migrationBuilder.DropForeignKey(
                name: "FK_Coaches_Users_UserId",
                schema: "core",
                table: "Coaches");

            // Drop index
            migrationBuilder.DropIndex(
                name: "IX_MemberPackages_CoachId",
                schema: "membership",
                table: "MemberPackages");

            // Drop columns
            migrationBuilder.DropColumn(
                name: "CoachId",
                schema: "membership",
                table: "MemberPackages");

            migrationBuilder.DropColumn(
                name: "IsPrivateTraining",
                schema: "membership",
                table: "PackageDefinitions");

            migrationBuilder.DropColumn(
                name: "CoachTarget",
                schema: "core",
                table: "Coaches");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "core",
                table: "Coaches");
        }
    }
}
