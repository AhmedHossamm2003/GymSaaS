using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymSaaS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaidDropIns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OneClassPassPrice",
                schema: "core",
                table: "Tenants",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 450m);

            migrationBuilder.AddColumn<decimal>(
                name: "OpenGymDropInPrice",
                schema: "core",
                table: "Tenants",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 550m);

            migrationBuilder.AddColumn<Guid>(
                name: "AttendanceRecordId",
                schema: "finance",
                table: "ManualIncomeEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseAmount",
                schema: "finance",
                table: "ManualIncomeEntries",
                type: "decimal(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GymClassId",
                schema: "finance",
                table: "ManualIncomeEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MemberId",
                schema: "finance",
                table: "ManualIncomeEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceOverrideReason",
                schema: "finance",
                table: "ManualIncomeEntries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceCode",
                schema: "finance",
                table: "ManualIncomeEntries",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                schema: "finance",
                table: "ManualIncomeEntries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAtUtc",
                schema: "finance",
                table: "ManualIncomeEntries",
                type: "datetime2(0)",
                precision: 0,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedByUserId",
                schema: "finance",
                table: "ManualIncomeEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WasMemberCreated",
                schema: "finance",
                table: "ManualIncomeEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "GymClassId",
                schema: "attendance",
                table: "AttendanceRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_GymClassId",
                schema: "attendance",
                table: "AttendanceRecords",
                column: "GymClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_GymClasses",
                schema: "attendance",
                table: "AttendanceRecords",
                column: "GymClassId",
                principalSchema: "core",
                principalTable: "GymClasses",
                principalColumn: "GymClassId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceRecords_GymClasses",
                schema: "attendance",
                table: "AttendanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_GymClassId",
                schema: "attendance",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "OneClassPassPrice",
                schema: "core",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OpenGymDropInPrice",
                schema: "core",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "AttendanceRecordId",
                schema: "finance",
                table: "ManualIncomeEntries");

            migrationBuilder.DropColumn(
                name: "BaseAmount",
                schema: "finance",
                table: "ManualIncomeEntries");

            migrationBuilder.DropColumn(
                name: "GymClassId",
                schema: "finance",
                table: "ManualIncomeEntries");

            migrationBuilder.DropColumn(
                name: "MemberId",
                schema: "finance",
                table: "ManualIncomeEntries");

            migrationBuilder.DropColumn(
                name: "PriceOverrideReason",
                schema: "finance",
                table: "ManualIncomeEntries");

            migrationBuilder.DropColumn(
                name: "SourceCode",
                schema: "finance",
                table: "ManualIncomeEntries");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                schema: "finance",
                table: "ManualIncomeEntries");

            migrationBuilder.DropColumn(
                name: "VoidedAtUtc",
                schema: "finance",
                table: "ManualIncomeEntries");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                schema: "finance",
                table: "ManualIncomeEntries");

            migrationBuilder.DropColumn(
                name: "WasMemberCreated",
                schema: "finance",
                table: "ManualIncomeEntries");

            migrationBuilder.DropColumn(
                name: "GymClassId",
                schema: "attendance",
                table: "AttendanceRecords");
        }
    }
}
