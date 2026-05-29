-- Migration: Add Capacity to core.Branches
-- Run once against your database before deploying.

ALTER TABLE [core].[Branches]
    ADD [Capacity] INT NULL;
GO
