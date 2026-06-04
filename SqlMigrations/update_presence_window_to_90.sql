-- ================================================================
-- Migration: shorten member presence window from 120 → 90 minutes.
-- Reasoning: the "still inside" gym window should be 1.5 h, while
-- re-entry cooldown (separate, application-level 8 h) controls when
-- a member can scan back in.
-- ================================================================

UPDATE [core].[Branches]
SET    [MemberPresenceWindowMinutes] = 90
WHERE  [MemberPresenceWindowMinutes] = 120;
GO
