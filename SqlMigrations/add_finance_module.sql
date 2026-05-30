-- ================================================================
-- Migration: Add Finance module (Expenses + Manual Income Entries)
-- Run once against your database before deploying the new code.
-- Safe to re-run (uses IF NOT EXISTS / IF NOT EXISTS-style guards).
-- ================================================================

-- 1. Create finance schema if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'finance')
BEGIN
    EXEC('CREATE SCHEMA [finance]');
    PRINT 'Schema [finance] created.';
END
ELSE
BEGIN
    PRINT 'Schema [finance] already exists. Skipped.';
END
GO

-- 2. Create Expenses table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Expenses' AND schema_id = SCHEMA_ID('finance'))
BEGIN
    CREATE TABLE [finance].[Expenses] (
        [ExpenseId]        UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
        [TenantId]         UNIQUEIDENTIFIER NOT NULL,
        [BranchId]         UNIQUEIDENTIFIER NULL,
        [CategoryCode]     NVARCHAR(40)     NOT NULL,
        [VendorName]       NVARCHAR(200)    NULL,
        [Amount]           DECIMAL(12, 2)   NOT NULL,
        [ExpenseDate]      DATE             NOT NULL,
        [Notes]            NVARCHAR(1000)   NULL,
        [PaymentMethod]    NVARCHAR(40)     NULL,  -- CASH / CARD / BANK / OTHER

        [CreatedAtUtc]     DATETIME2(0)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId]  UNIQUEIDENTIFIER NULL,
        [UpdatedAtUtc]     DATETIME2(0)     NULL,
        [UpdatedByUserId]  UNIQUEIDENTIFIER NULL,
        [IsDeleted]        BIT              NOT NULL DEFAULT (0),

        CONSTRAINT [PK_Expenses]
            PRIMARY KEY ([ExpenseId]),

        CONSTRAINT [FK_Expenses_Tenants]
            FOREIGN KEY ([TenantId])
            REFERENCES [core].[Tenants] ([TenantId]),

        CONSTRAINT [FK_Expenses_Branches]
            FOREIGN KEY ([BranchId])
            REFERENCES [core].[Branches] ([BranchId]),

        CONSTRAINT [FK_Expenses_CreatedBy]
            FOREIGN KEY ([CreatedByUserId])
            REFERENCES [identityx].[Users] ([UserId]),

        CONSTRAINT [FK_Expenses_UpdatedBy]
            FOREIGN KEY ([UpdatedByUserId])
            REFERENCES [identityx].[Users] ([UserId])
    );

    CREATE INDEX [IX_Expenses_TenantId_ExpenseDate]
        ON [finance].[Expenses] ([TenantId], [ExpenseDate] DESC);

    CREATE INDEX [IX_Expenses_BranchId]
        ON [finance].[Expenses] ([BranchId]);

    CREATE INDEX [IX_Expenses_CategoryCode]
        ON [finance].[Expenses] ([CategoryCode]);

    PRINT 'Table [finance].[Expenses] created.';
END
ELSE
BEGIN
    PRINT 'Table [finance].[Expenses] already exists. Skipped.';
END
GO

-- 3. Create ManualIncomeEntries table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ManualIncomeEntries' AND schema_id = SCHEMA_ID('finance'))
BEGIN
    CREATE TABLE [finance].[ManualIncomeEntries] (
        [IncomeEntryId]    UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
        [TenantId]         UNIQUEIDENTIFIER NOT NULL,
        [BranchId]         UNIQUEIDENTIFIER NULL,
        [CategoryCode]     NVARCHAR(40)     NOT NULL,
        [Description]      NVARCHAR(200)    NOT NULL,
        [Amount]           DECIMAL(12, 2)   NOT NULL,
        [IncomeDate]       DATE             NOT NULL,
        [Notes]            NVARCHAR(1000)   NULL,
        [PaymentMethod]    NVARCHAR(40)     NULL,

        [CreatedAtUtc]     DATETIME2(0)     NOT NULL DEFAULT (SYSUTCDATETIME()),
        [CreatedByUserId]  UNIQUEIDENTIFIER NULL,
        [UpdatedAtUtc]     DATETIME2(0)     NULL,
        [UpdatedByUserId]  UNIQUEIDENTIFIER NULL,
        [IsDeleted]        BIT              NOT NULL DEFAULT (0),

        CONSTRAINT [PK_ManualIncomeEntries]
            PRIMARY KEY ([IncomeEntryId]),

        CONSTRAINT [FK_ManualIncomeEntries_Tenants]
            FOREIGN KEY ([TenantId])
            REFERENCES [core].[Tenants] ([TenantId]),

        CONSTRAINT [FK_ManualIncomeEntries_Branches]
            FOREIGN KEY ([BranchId])
            REFERENCES [core].[Branches] ([BranchId]),

        CONSTRAINT [FK_ManualIncomeEntries_CreatedBy]
            FOREIGN KEY ([CreatedByUserId])
            REFERENCES [identityx].[Users] ([UserId]),

        CONSTRAINT [FK_ManualIncomeEntries_UpdatedBy]
            FOREIGN KEY ([UpdatedByUserId])
            REFERENCES [identityx].[Users] ([UserId])
    );

    CREATE INDEX [IX_ManualIncomeEntries_TenantId_IncomeDate]
        ON [finance].[ManualIncomeEntries] ([TenantId], [IncomeDate] DESC);

    CREATE INDEX [IX_ManualIncomeEntries_BranchId]
        ON [finance].[ManualIncomeEntries] ([BranchId]);

    CREATE INDEX [IX_ManualIncomeEntries_CategoryCode]
        ON [finance].[ManualIncomeEntries] ([CategoryCode]);

    PRINT 'Table [finance].[ManualIncomeEntries] created.';
END
ELSE
BEGIN
    PRINT 'Table [finance].[ManualIncomeEntries] already exists. Skipped.';
END
GO

PRINT '================================================================';
PRINT 'Finance module migration complete.';
PRINT '================================================================';
