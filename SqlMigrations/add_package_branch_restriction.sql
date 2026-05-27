-- Migration: Add RestrictedToBranchId to membership.PackageDefinitions
-- Run once against your database before deploying.

ALTER TABLE [membership].[PackageDefinitions]
    ADD [RestrictedToBranchId] UNIQUEIDENTIFIER NULL;
GO

ALTER TABLE [membership].[PackageDefinitions]
    ADD CONSTRAINT [FK_PackageDefinitions_RestrictedBranch]
        FOREIGN KEY ([RestrictedToBranchId])
        REFERENCES [core].[Branches] ([BranchId]);
GO

CREATE INDEX [IX_PackageDefinitions_RestrictedToBranchId]
    ON [membership].[PackageDefinitions] ([RestrictedToBranchId])
    WHERE [RestrictedToBranchId] IS NOT NULL;
GO
