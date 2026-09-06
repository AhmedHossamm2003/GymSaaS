IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260608133322_AddCoachAndPrivateTrainingFields'
)
BEGIN
    ALTER TABLE [core].[Coaches] ADD [UserId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260608133322_AddCoachAndPrivateTrainingFields'
)
BEGIN
    ALTER TABLE [core].[Coaches] ADD [CoachTarget] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260608133322_AddCoachAndPrivateTrainingFields'
)
BEGIN
    ALTER TABLE [core].[Coaches] ADD CONSTRAINT [FK_Coaches_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [identityx].[Users] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260608133322_AddCoachAndPrivateTrainingFields'
)
BEGIN
    ALTER TABLE [membership].[PackageDefinitions] ADD [IsPrivateTraining] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260608133322_AddCoachAndPrivateTrainingFields'
)
BEGIN
    ALTER TABLE [membership].[MemberPackages] ADD [CoachId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260608133322_AddCoachAndPrivateTrainingFields'
)
BEGIN
    ALTER TABLE [membership].[MemberPackages] ADD CONSTRAINT [FK_MemberPackages_Coaches_CoachId] FOREIGN KEY ([CoachId]) REFERENCES [core].[Coaches] ([CoachId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260608133322_AddCoachAndPrivateTrainingFields'
)
BEGIN
    CREATE INDEX [IX_MemberPackages_CoachId] ON [membership].[MemberPackages] ([CoachId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260608133322_AddCoachAndPrivateTrainingFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260608133322_AddCoachAndPrivateTrainingFields', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260609022244_AddCoachCommission'
)
BEGIN
    ALTER TABLE [membership].[PackageDefinitions] ADD [CoachCommissionPercent] decimal(5,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260609022244_AddCoachCommission'
)
BEGIN
    ALTER TABLE [membership].[MemberPackages] ADD [CoachCommissionAmount] decimal(10,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260609022244_AddCoachCommission'
)
BEGIN
    ALTER TABLE [membership].[MemberPackages] ADD [CoachCommissionPercent] decimal(5,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260609022244_AddCoachCommission'
)
BEGIN
    ALTER TABLE [membership].[MemberPackages] ADD [PriceSnapshot] decimal(10,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260609022244_AddCoachCommission'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260609022244_AddCoachCommission', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610180157_AddMaxDiscountedPrice'
)
BEGIN
    ALTER TABLE [membership].[PackageDefinitions] ADD [MaxDiscountedPrice] decimal(10,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610180157_AddMaxDiscountedPrice'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610180157_AddMaxDiscountedPrice', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610200611_AddMemberPerkUsages'
)
BEGIN
    CREATE TABLE [membership].[MemberPerkUsages] (
        [PerkUsageId] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [MemberId] uniqueidentifier NOT NULL,
        [MemberPackageId] uniqueidentifier NULL,
        [PerkType] nvarchar(20) NOT NULL,
        [CoachId] uniqueidentifier NULL,
        [BranchId] uniqueidentifier NOT NULL,
        [UsedAtUtc] datetime2(0) NOT NULL,
        [RecordedByUserId] uniqueidentifier NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_MemberPerkUsages] PRIMARY KEY ([PerkUsageId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610200611_AddMemberPerkUsages'
)
BEGIN
    CREATE INDEX [IX_MemberPerkUsages_Branch_UsedAt] ON [membership].[MemberPerkUsages] ([BranchId], [UsedAtUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610200611_AddMemberPerkUsages'
)
BEGIN
    CREATE INDEX [IX_MemberPerkUsages_MemberId] ON [membership].[MemberPerkUsages] ([MemberId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610200611_AddMemberPerkUsages'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610200611_AddMemberPerkUsages', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905080626_AddPersonalTrainingPlans'
)
BEGIN
    IF EXISTS (
        SELECT 1
        FROM [membership].[MemberPackages] mp
        INNER JOIN [membership].[PackageTypes] pt ON pt.[PackageTypeId] = mp.[PackageTypeId]
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
    INNER JOIN [membership].[PackageTypes] pt ON pt.[PackageTypeId] = pd.[PackageTypeId]
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
    END;

    DECLARE @ptPrivateDefault sysname;
    SELECT @ptPrivateDefault = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c]
        ON [d].[parent_column_id] = [c].[column_id]
       AND [d].[parent_object_id] = [c].[object_id]
    WHERE [d].[parent_object_id] = OBJECT_ID(N'[membership].[PackageDefinitions]')
      AND [c].[name] = N'IsPrivateTraining';
    IF @ptPrivateDefault IS NOT NULL
        EXEC(N'ALTER TABLE [membership].[PackageDefinitions] DROP CONSTRAINT [' + @ptPrivateDefault + '];');

    IF COL_LENGTH(N'membership.PackageDefinitions', N'IsPrivateTraining') IS NOT NULL
        ALTER TABLE [membership].[PackageDefinitions] DROP COLUMN [IsPrivateTraining];

    IF COL_LENGTH(N'membership.MemberPackages', N'CoachCommissionAmount') IS NOT NULL
        ALTER TABLE [membership].[MemberPackages] DROP COLUMN [CoachCommissionAmount];

    IF COL_LENGTH(N'membership.MemberPerkUsages', N'AttendanceRecordId') IS NULL
        ALTER TABLE [membership].[MemberPerkUsages] ADD [AttendanceRecordId] uniqueidentifier NULL;
    IF COL_LENGTH(N'membership.MemberPerkUsages', N'CommissionAmount') IS NULL
        ALTER TABLE [membership].[MemberPerkUsages] ADD [CommissionAmount] decimal(10,2) NULL;
    IF COL_LENGTH(N'membership.MemberPerkUsages', N'CommissionPercentSnapshot') IS NULL
        ALTER TABLE [membership].[MemberPerkUsages] ADD [CommissionPercentSnapshot] decimal(5,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260905080626_AddPersonalTrainingPlans'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260905080626_AddPersonalTrainingPlans', N'8.0.0');
END;
GO

COMMIT;
GO


