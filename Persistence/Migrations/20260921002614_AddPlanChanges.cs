using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymSaaS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PlanChangeAmount",
                schema: "membership",
                table: "MemberPackages",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlanChangedFromMemberPackageId",
                schema: "membership",
                table: "MemberPackages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberPackages_PlanChangedFromMemberPackageId",
                schema: "membership",
                table: "MemberPackages",
                column: "PlanChangedFromMemberPackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_MemberPackages_MemberPackages_PlanChangedFromMemberPackageId",
                schema: "membership",
                table: "MemberPackages",
                column: "PlanChangedFromMemberPackageId",
                principalSchema: "membership",
                principalTable: "MemberPackages",
                principalColumn: "MemberPackageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MemberPackages_MemberPackages_PlanChangedFromMemberPackageId",
                schema: "membership",
                table: "MemberPackages");

            migrationBuilder.DropIndex(
                name: "IX_MemberPackages_PlanChangedFromMemberPackageId",
                schema: "membership",
                table: "MemberPackages");

            migrationBuilder.DropColumn(
                name: "PlanChangeAmount",
                schema: "membership",
                table: "MemberPackages");

            migrationBuilder.DropColumn(
                name: "PlanChangedFromMemberPackageId",
                schema: "membership",
                table: "MemberPackages");
        }
    }
}
