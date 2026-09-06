using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymSaaS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalTrainingPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [membership].[MemberPackages] mp
                    INNER JOIN [membership].[PackageTypes] pt
                        ON pt.[PackageTypeId] = mp.[PackageTypeId]
                    WHERE pt.[PackageTypeCode] = N'COMBINED'
                )
                    THROW 51000, 'Cannot remove COMBINED while member assignments still reference it.', 1;

                UPDATE mp
                SET mp.[PackageDefinitionId] = NULL
                FROM [membership].[MemberPackages] mp
                INNER JOIN [membership].[PackageDefinitions] pd
                    ON pd.[PackageDefinitionId] = mp.[PackageDefinitionId]
                INNER JOIN [membership].[PackageTypes] pt
                    ON pt.[PackageTypeId] = pd.[PackageTypeId]
                WHERE pt.[PackageTypeCode] = N'COMBINED';

                DELETE pd
                FROM [membership].[PackageDefinitions] pd
                INNER JOIN [membership].[PackageTypes] pt
                    ON pt.[PackageTypeId] = pd.[PackageTypeId]
                WHERE pt.[PackageTypeCode] = N'COMBINED';

                DELETE FROM [membership].[PackageTypes]
                WHERE [PackageTypeCode] = N'COMBINED';

                IF NOT EXISTS (
                    SELECT 1 FROM [membership].[PackageTypes]
                    WHERE [PackageTypeCode] = N'PERSONAL_TRAINING'
                )
                BEGIN
                    INSERT INTO [membership].[PackageTypes]
                        ([PackageTypeId], [PackageTypeCode], [PackageTypeName], [Description])
                    VALUES
                        (NEWID(), N'PERSONAL_TRAINING', N'Personal Training',
                         N'Coach-led sessions with attendance and commission recorded per delivered session.');
                END
                """);

            migrationBuilder.DropColumn(
                name: "IsPrivateTraining",
                schema: "membership",
                table: "PackageDefinitions");

            migrationBuilder.DropColumn(
                name: "CoachCommissionAmount",
                schema: "membership",
                table: "MemberPackages");

            migrationBuilder.AddColumn<Guid>(
                name: "AttendanceRecordId",
                schema: "membership",
                table: "MemberPerkUsages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionAmount",
                schema: "membership",
                table: "MemberPerkUsages",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionPercentSnapshot",
                schema: "membership",
                table: "MemberPerkUsages",
                type: "decimal(5,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [membership].[PackageTypes]
                WHERE [PackageTypeCode] = N'PERSONAL_TRAINING'
                  AND NOT EXISTS (
                      SELECT 1 FROM [membership].[PackageDefinitions] pd
                      WHERE pd.[PackageTypeId] = [membership].[PackageTypes].[PackageTypeId]
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM [membership].[MemberPackages] mp
                      WHERE mp.[PackageTypeId] = [membership].[PackageTypes].[PackageTypeId]
                  );

                IF NOT EXISTS (
                    SELECT 1 FROM [membership].[PackageTypes]
                    WHERE [PackageTypeCode] = N'COMBINED'
                )
                BEGIN
                    INSERT INTO [membership].[PackageTypes]
                        ([PackageTypeId], [PackageTypeCode], [PackageTypeName], [Description])
                    VALUES
                        (NEWID(), N'COMBINED', N'Combined',
                         N'Sessions and open gym access with separate expiry dates.');
                END
                """);

            migrationBuilder.DropColumn(
                name: "AttendanceRecordId",
                schema: "membership",
                table: "MemberPerkUsages");

            migrationBuilder.DropColumn(
                name: "CommissionAmount",
                schema: "membership",
                table: "MemberPerkUsages");

            migrationBuilder.DropColumn(
                name: "CommissionPercentSnapshot",
                schema: "membership",
                table: "MemberPerkUsages");

            migrationBuilder.AddColumn<bool>(
                name: "IsPrivateTraining",
                schema: "membership",
                table: "PackageDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "CoachCommissionAmount",
                schema: "membership",
                table: "MemberPackages",
                type: "decimal(10,2)",
                nullable: true);
        }
    }
}
