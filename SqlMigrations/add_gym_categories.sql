-- Migration: Add gym bodypart split categories (IDs 8-12)
-- Run once against your database before deploying.

INSERT INTO [catalog].[ExerciseCategories] ([ExerciseCategoryId], [CategoryName], [IconClass], [IsActive]) VALUES
(8,  'Chest',     'bi-plus-circle',       1),
(9,  'Back',      'bi-arrow-up-circle',   1),
(10, 'Shoulders', 'bi-arrow-up',          1),
(11, 'Arms',      'bi-hand-index-thumb',  1),
(12, 'Legs',      'bi-person-walking',    1);
GO
