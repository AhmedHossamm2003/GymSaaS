using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymSaaS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberPerkUsages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemberPerkUsages",
                schema: "membership",
                columns: table => new
                {
                    PerkUsageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PerkType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2(0)", precision: 0, nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberPerkUsages", x => x.PerkUsageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberPerkUsages_Branch_UsedAt",
                schema: "membership",
                table: "MemberPerkUsages",
                columns: new[] { "BranchId", "UsedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberPerkUsages_MemberId",
                schema: "membership",
                table: "MemberPerkUsages",
                column: "MemberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberPerkUsages",
                schema: "membership");
        }
    }
}
