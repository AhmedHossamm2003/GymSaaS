/*
    IDEA WELLNESS — SHERATON PACKAGE CATALOG

    Safe to rerun:
      - Existing rows with the same PackageCode are updated.
      - Missing rows are inserted.
      - No member package assignments are created or changed.

    Set @ApplyChanges to 0 for preview, then to 1 to save.

    PT packages are intentionally inserted as inactive drafts because their
    validity and coach commission are still undecided. Edit and activate them
    from Package Catalog after both values are known.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ApplyChanges bit = 0;
DECLARE @TenantCode nvarchar(50) = N'GYM001';
DECLARE @BranchName nvarchar(200) = N'Sheraton branch';

DECLARE @TenantId uniqueidentifier;
DECLARE @BranchId uniqueidentifier;
DECLARE @HomeOnlyPolicyId uniqueidentifier;

SELECT @TenantId = [TenantId]
FROM [core].[Tenants]
WHERE [TenantCode] = @TenantCode;

IF @TenantId IS NULL
    THROW 51000, 'Tenant GYM001 was not found.', 1;

IF (
    SELECT COUNT(*)
    FROM [core].[Branches]
    WHERE [TenantId] = @TenantId
      AND [IsActive] = 1
      AND LTRIM(RTRIM([BranchName])) = @BranchName
) <> 1
BEGIN
    SELECT [BranchCode], [BranchName], [IsActive]
    FROM [core].[Branches]
    WHERE [TenantId] = @TenantId
    ORDER BY [BranchName];

    THROW 51001,
        'Exactly one active Sheraton branch was not found. Update @BranchName.',
        1;
END;

SELECT @BranchId = [BranchId]
FROM [core].[Branches]
WHERE [TenantId] = @TenantId
  AND [IsActive] = 1
  AND LTRIM(RTRIM([BranchName])) = @BranchName;

SELECT @HomeOnlyPolicyId = [BranchAccessPolicyTypeId]
FROM [membership].[BranchAccessPolicyTypes]
WHERE [PolicyCode] = N'HOME_ONLY';

IF @HomeOnlyPolicyId IS NULL
    THROW 51002, 'HOME_ONLY branch policy was not found.', 1;

IF EXISTS (
    SELECT required.[PackageTypeCode]
    FROM (VALUES (N'SESSION'), (N'OPEN_GYM'), (N'PERSONAL_TRAINING'))
         required([PackageTypeCode])
    WHERE NOT EXISTS (
        SELECT 1
        FROM [membership].[PackageTypes] existing
        WHERE existing.[PackageTypeCode] = required.[PackageTypeCode]
    )
)
    THROW 51003, 'One or more required package types are missing.', 1;

DECLARE @Packages table
(
    [PackageCode] nvarchar(50) NOT NULL PRIMARY KEY,
    [PackageName] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [PackageTypeCode] nvarchar(30) NOT NULL,
    [SessionCount] int NULL,
    [DurationDays] int NULL,
    [Price] decimal(10,2) NOT NULL,
    [IsActive] bit NOT NULL,
    [SortOrder] int NOT NULL
);

INSERT INTO @Packages
    ([PackageCode], [PackageName], [Description], [PackageTypeCode],
     [SessionCount], [DurationDays], [Price], [IsActive], [SortOrder])
VALUES
    (N'GRP-10',  N'10 Group Sessions',
     N'10 non-shareable group-class sessions valid for 30 days at Sheraton branch.',
     N'SESSION', 10, 30, 3500.00, 1, 10),

    (N'GRP-25',  N'25 Group Sessions',
     N'25 non-shareable group-class sessions valid for 90 days at Sheraton branch.',
     N'SESSION', 25, 90, 7000.00, 1, 20),

    (N'GRP-50',  N'50 Group Sessions',
     N'50 non-shareable group-class sessions valid for 180 days at Sheraton branch.',
     N'SESSION', 50, 180, 12500.00, 1, 30),

    (N'GRP-100', N'100 Group Sessions',
     N'100 non-shareable group-class sessions valid for 365 days at Sheraton branch.',
     N'SESSION', 100, 365, 20000.00, 1, 40),

    (N'GRP-250', N'250 Group Sessions',
     N'250 non-shareable group-class sessions valid for 365 days at Sheraton branch.',
     N'SESSION', 250, 365, 37500.00, 1, 50),

    (N'GYM-1M',  N'1 Month Gym',
     N'Open-gym access for 30 days at Sheraton branch.',
     N'OPEN_GYM', NULL, 30, 3000.00, 1, 110),

    (N'GYM-3M',  N'3 Months Gym',
     N'Open-gym access for 90 days at Sheraton branch.',
     N'OPEN_GYM', NULL, 90, 7000.00, 1, 120),

    (N'GYM-6M',  N'6 Months Gym',
     N'Open-gym access for 180 days at Sheraton branch.',
     N'OPEN_GYM', NULL, 180, 10000.00, 1, 130),

    (N'GYM-14M', N'14 Months Gym',
     N'One-year gym offer with two bonus months: 420 days at Sheraton branch.',
     N'OPEN_GYM', NULL, 420, 16000.00, 1, 140),

    (N'PT-8',  N'PT 8 Sessions',
     N'Personal training plan. Set validity and coach commission before activation.',
     N'PERSONAL_TRAINING', 8, NULL, 4500.00, 0, 210),

    (N'PT-15', N'PT 15 Sessions',
     N'Personal training plan. Set validity and coach commission before activation.',
     N'PERSONAL_TRAINING', 15, NULL, 6600.00, 0, 220),

    (N'PT-20', N'PT 20 Sessions',
     N'Personal training plan. Set validity and coach commission before activation.',
     N'PERSONAL_TRAINING', 20, NULL, 8800.00, 0, 230);

/* Preview */
SELECT
    CASE WHEN existing.[PackageDefinitionId] IS NULL
         THEN N'INSERT'
         ELSE N'UPDATE'
    END AS [Action],
    seed.[PackageCode],
    seed.[PackageName],
    seed.[PackageTypeCode],
    seed.[SessionCount],
    seed.[DurationDays],
    seed.[Price],
    seed.[IsActive],
    @BranchName AS [RestrictedBranch],
    N'HOME_ONLY' AS [BranchPolicy]
FROM @Packages seed
LEFT JOIN [membership].[PackageDefinitions] existing
    ON existing.[TenantId] = @TenantId
   AND existing.[PackageCode] = seed.[PackageCode]
ORDER BY seed.[SortOrder];

IF @ApplyChanges = 0
BEGIN
    PRINT 'PREVIEW ONLY: no packages were inserted or updated.';
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    /* Update matching package codes. */
    UPDATE existing
    SET
        existing.[PackageName] = seed.[PackageName],
        existing.[Description] = seed.[Description],
        existing.[PackageTypeId] = packageType.[PackageTypeId],
        existing.[BranchAccessPolicyTypeId] = @HomeOnlyPolicyId,
        existing.[SessionCount] = seed.[SessionCount],
        existing.[GymClassId] = NULL,
        existing.[InvitationCount] = NULL,
        existing.[InBodyCount] = NULL,
        existing.[FreezeAllowanceDays] = NULL,
        existing.[DurationDays] = seed.[DurationDays],
        existing.[CrossBranchVisitLimit] = NULL,
        existing.[DailyAttendanceLimit] = NULL,
        existing.[WeeklyAttendanceLimit] = NULL,
        existing.[MonthlyAttendanceLimit] = NULL,
        existing.[AllowCarryOverSessions] = 0,
        existing.[AllowQueuedRenewal] = 1,
        existing.[AllowCustomOverrideDuringAssignment] = 1,
        existing.[IsCustomTemplate] = 0,
        existing.[IsActive] = seed.[IsActive],
        existing.[SortOrder] = seed.[SortOrder],
        existing.[UpdatedAtUtc] = SYSUTCDATETIME(),
        existing.[UpdatedByUserId] = NULL,
        existing.[OpenGymDurationDays] = NULL,
        existing.[OpenGymDailyLimit] = 1,
        existing.[Price] = seed.[Price],
        existing.[MaxDiscountedPrice] = NULL,
        existing.[CoachCommissionPercent] = NULL,
        existing.[RestrictedToBranchId] = @BranchId
    FROM [membership].[PackageDefinitions] existing
    INNER JOIN @Packages seed
        ON seed.[PackageCode] = existing.[PackageCode]
    INNER JOIN [membership].[PackageTypes] packageType
        ON packageType.[PackageTypeCode] = seed.[PackageTypeCode]
    WHERE existing.[TenantId] = @TenantId;

    /* Insert package codes that do not yet exist. */
    INSERT INTO [membership].[PackageDefinitions]
    (
        [PackageDefinitionId], [TenantId], [PackageCode], [PackageName],
        [Description], [PackageTypeId], [BranchAccessPolicyTypeId],
        [SessionCount], [GymClassId], [InvitationCount], [InBodyCount],
        [FreezeAllowanceDays], [DurationDays], [CrossBranchVisitLimit],
        [DailyAttendanceLimit], [WeeklyAttendanceLimit],
        [MonthlyAttendanceLimit], [AllowCarryOverSessions],
        [AllowQueuedRenewal], [AllowCustomOverrideDuringAssignment],
        [IsCustomTemplate], [IsActive], [SortOrder], [CreatedAtUtc],
        [CreatedByUserId], [UpdatedAtUtc], [UpdatedByUserId],
        [OpenGymDurationDays], [OpenGymDailyLimit], [Price],
        [MaxDiscountedPrice], [CoachCommissionPercent], [RestrictedToBranchId]
    )
    SELECT
        NEWID(), @TenantId, seed.[PackageCode], seed.[PackageName],
        seed.[Description], packageType.[PackageTypeId], @HomeOnlyPolicyId,
        seed.[SessionCount], NULL, NULL, NULL,
        NULL, seed.[DurationDays], NULL,
        NULL, NULL,
        NULL, 0,
        1, 1,
        0, seed.[IsActive], seed.[SortOrder], SYSUTCDATETIME(),
        NULL, NULL, NULL,
        NULL, 1, seed.[Price],
        NULL, NULL, @BranchId
    FROM @Packages seed
    INNER JOIN [membership].[PackageTypes] packageType
        ON packageType.[PackageTypeCode] = seed.[PackageTypeCode]
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM [membership].[PackageDefinitions] existing
        WHERE existing.[TenantId] = @TenantId
          AND existing.[PackageCode] = seed.[PackageCode]
    );

    COMMIT TRANSACTION;

    SELECT
        package.[PackageCode],
        package.[PackageName],
        packageType.[PackageTypeCode],
        package.[SessionCount],
        package.[DurationDays],
        package.[Price],
        package.[CoachCommissionPercent],
        package.[IsActive],
        branch.[BranchName] AS [RestrictedBranch],
        policy.[PolicyCode] AS [BranchPolicy]
    FROM [membership].[PackageDefinitions] package
    INNER JOIN [membership].[PackageTypes] packageType
        ON packageType.[PackageTypeId] = package.[PackageTypeId]
    INNER JOIN [membership].[BranchAccessPolicyTypes] policy
        ON policy.[BranchAccessPolicyTypeId] = package.[BranchAccessPolicyTypeId]
    LEFT JOIN [core].[Branches] branch
        ON branch.[BranchId] = package.[RestrictedToBranchId]
    WHERE package.[TenantId] = @TenantId
      AND package.[PackageCode] IN (SELECT [PackageCode] FROM @Packages)
    ORDER BY package.[SortOrder];

    PRINT 'Idea Wellness package catalog seeded successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
