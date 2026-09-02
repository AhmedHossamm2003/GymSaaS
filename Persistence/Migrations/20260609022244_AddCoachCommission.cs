using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymSaaS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachCommission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CoachCommissionPercent",
                schema: "membership",
                table: "PackageDefinitions",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CoachCommissionAmount",
                schema: "membership",
                table: "MemberPackages",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CoachCommissionPercent",
                schema: "membership",
                table: "MemberPackages",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceSnapshot",
                schema: "membership",
                table: "MemberPackages",
                type: "decimal(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoachCommissionPercent",
                schema: "membership",
                table: "PackageDefinitions");

            migrationBuilder.DropColumn(
                name: "CoachCommissionAmount",
                schema: "membership",
                table: "MemberPackages");

            migrationBuilder.DropColumn(
                name: "CoachCommissionPercent",
                schema: "membership",
                table: "MemberPackages");

            migrationBuilder.DropColumn(
                name: "PriceSnapshot",
                schema: "membership",
                table: "MemberPackages");
        }
    }
}
