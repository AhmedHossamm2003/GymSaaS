-- Migration: Add Exercise Library (catalog schema)
-- Run once against your database before deploying.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'catalog')
    EXEC('CREATE SCHEMA [catalog]');
GO

-- ExerciseCategories (seeded reference data)
CREATE TABLE [catalog].[ExerciseCategories] (
    [ExerciseCategoryId]  INT           NOT NULL,
    [CategoryName]        NVARCHAR(100) NOT NULL,
    [IconClass]           NVARCHAR(50)  NULL,
    [IsActive]            BIT           NOT NULL DEFAULT (1),
    CONSTRAINT [PK_ExerciseCategories] PRIMARY KEY ([ExerciseCategoryId])
);
GO

INSERT INTO [catalog].[ExerciseCategories] ([ExerciseCategoryId], [CategoryName], [IconClass], [IsActive]) VALUES
(1, 'Strength',        'bi-lightning-charge',  1),
(2, 'Cardio',          'bi-heart-pulse',        1),
(3, 'Flexibility',     'bi-arrow-left-right',   1),
(4, 'HIIT',            'bi-fire',               1),
(5, 'Core & Balance',  'bi-crosshair2',         1),
(6, 'Olympic Lifting', 'bi-trophy',             1),
(7, 'Bodyweight',      'bi-person-arms-up',     1);
GO

-- MuscleGroups (seeded reference data)
CREATE TABLE [catalog].[MuscleGroups] (
    [MuscleGroupId]    INT           NOT NULL,
    [MuscleGroupName]  NVARCHAR(100) NOT NULL,
    [BodyPart]         NVARCHAR(50)  NOT NULL,
    CONSTRAINT [PK_MuscleGroups] PRIMARY KEY ([MuscleGroupId])
);
GO

INSERT INTO [catalog].[MuscleGroups] ([MuscleGroupId], [MuscleGroupName], [BodyPart]) VALUES
(1,  'Chest',       'Upper Body'),
(2,  'Back',        'Upper Body'),
(3,  'Shoulders',   'Upper Body'),
(4,  'Biceps',      'Upper Body'),
(5,  'Triceps',     'Upper Body'),
(6,  'Forearms',    'Upper Body'),
(7,  'Abs',         'Core'),
(8,  'Obliques',    'Core'),
(9,  'Lower Back',  'Core'),
(10, 'Quads',       'Lower Body'),
(11, 'Hamstrings',  'Lower Body'),
(12, 'Glutes',      'Lower Body'),
(13, 'Calves',      'Lower Body'),
(14, 'Hip Flexors', 'Lower Body'),
(15, 'Full Body',   'Full Body');
GO

-- Exercises
CREATE TABLE [catalog].[Exercises] (
    [ExerciseId]          UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [TenantId]            UNIQUEIDENTIFIER NOT NULL,
    [ExerciseName]        NVARCHAR(200)    NOT NULL,
    [Description]         NVARCHAR(MAX)    NULL,
    [Instructions]        NVARCHAR(MAX)    NULL,
    [PhotoUrl]            NVARCHAR(500)    NULL,
    [VideoUrl]            NVARCHAR(500)    NULL,
    [Difficulty]          TINYINT          NOT NULL DEFAULT (1),
    [Equipment]           NVARCHAR(200)    NULL,
    [ExerciseCategoryId]  INT              NOT NULL,
    [IsActive]            BIT              NOT NULL DEFAULT (1),
    [IsDeleted]           BIT              NOT NULL DEFAULT (0),
    [CreatedAtUtc]        DATETIME2(0)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedAtUtc]        DATETIME2(0)     NULL,
    [CreatedByUserId]     UNIQUEIDENTIFIER NULL,
    [UpdatedByUserId]     UNIQUEIDENTIFIER NULL,

    CONSTRAINT [PK_Exercises]
        PRIMARY KEY ([ExerciseId]),

    CONSTRAINT [FK_Exercises_Tenants]
        FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenants]([TenantId]),

    CONSTRAINT [FK_Exercises_Categories]
        FOREIGN KEY ([ExerciseCategoryId]) REFERENCES [catalog].[ExerciseCategories]([ExerciseCategoryId]),

    CONSTRAINT [FK_Exercises_CreatedBy]
        FOREIGN KEY ([CreatedByUserId]) REFERENCES [identityx].[Users]([UserId]),

    CONSTRAINT [FK_Exercises_UpdatedBy]
        FOREIGN KEY ([UpdatedByUserId]) REFERENCES [identityx].[Users]([UserId])
);
GO

CREATE INDEX [IX_Exercises_TenantId]
    ON [catalog].[Exercises] ([TenantId]);
GO

-- ExerciseMuscleGroups (join table)
CREATE TABLE [catalog].[ExerciseMuscleGroups] (
    [ExerciseId]     UNIQUEIDENTIFIER NOT NULL,
    [MuscleGroupId]  INT              NOT NULL,
    [IsPrimary]      BIT              NOT NULL DEFAULT (1),

    CONSTRAINT [PK_ExerciseMuscleGroups]
        PRIMARY KEY ([ExerciseId], [MuscleGroupId]),

    CONSTRAINT [FK_ExerciseMuscleGroups_Exercise]
        FOREIGN KEY ([ExerciseId]) REFERENCES [catalog].[Exercises]([ExerciseId]) ON DELETE CASCADE,

    CONSTRAINT [FK_ExerciseMuscleGroups_MuscleGroup]
        FOREIGN KEY ([MuscleGroupId]) REFERENCES [catalog].[MuscleGroups]([MuscleGroupId])
);
GO
