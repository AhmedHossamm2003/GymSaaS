-- ================================================================
-- Migration: Link CLASS-type MemberPackages to a specific GymClass
-- ================================================================
-- A CLASS package can now be tied to one specific GymClass (e.g.
-- "Yoga Mondays 7pm"). NULL means "any class" (legacy behavior).
-- ================================================================

ALTER TABLE [membership].[MemberPackages]
ADD [GymClassId] UNIQUEIDENTIFIER NULL;
GO

ALTER TABLE [membership].[MemberPackages]
ADD CONSTRAINT [FK_MemberPackages_GymClasses]
    FOREIGN KEY ([GymClassId]) REFERENCES [core].[GymClasses] ([GymClassId]);
GO

CREATE INDEX [IX_MemberPackages_GymClassId]
    ON [membership].[MemberPackages] ([GymClassId])
    WHERE [GymClassId] IS NOT NULL;
GO
