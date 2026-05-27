-- Migration: Add Workout Templates (catalog schema)
-- Run once against your database before deploying.

CREATE TABLE [catalog].[WorkoutTemplates] (
    [WorkoutTemplateId]  UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [TenantId]           UNIQUEIDENTIFIER NOT NULL,
    [TemplateName]       NVARCHAR(200)    NOT NULL,
    [Description]        NVARCHAR(MAX)    NULL,
    [Difficulty]         TINYINT          NOT NULL DEFAULT (1),
    [EstimatedMinutes]   INT              NULL,
    [IsActive]           BIT              NOT NULL DEFAULT (1),
    [IsDeleted]          BIT              NOT NULL DEFAULT (0),
    [CreatedAtUtc]       DATETIME2(0)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    [UpdatedAtUtc]       DATETIME2(0)     NULL,
    [CreatedByUserId]    UNIQUEIDENTIFIER NULL,
    [UpdatedByUserId]    UNIQUEIDENTIFIER NULL,

    CONSTRAINT [PK_WorkoutTemplates]
        PRIMARY KEY ([WorkoutTemplateId]),

    CONSTRAINT [FK_WorkoutTemplates_Tenants]
        FOREIGN KEY ([TenantId]) REFERENCES [core].[Tenants]([TenantId]),

    CONSTRAINT [FK_WorkoutTemplates_CreatedBy]
        FOREIGN KEY ([CreatedByUserId]) REFERENCES [identityx].[Users]([UserId]),

    CONSTRAINT [FK_WorkoutTemplates_UpdatedBy]
        FOREIGN KEY ([UpdatedByUserId]) REFERENCES [identityx].[Users]([UserId])
);
GO

CREATE INDEX [IX_WorkoutTemplates_TenantId]
    ON [catalog].[WorkoutTemplates] ([TenantId]);
GO

CREATE TABLE [catalog].[WorkoutTemplateExercises] (
    [WorkoutTemplateExerciseId]  UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [WorkoutTemplateId]          UNIQUEIDENTIFIER NOT NULL,
    [ExerciseId]                 UNIQUEIDENTIFIER NOT NULL,
    [SortOrder]                  INT              NOT NULL DEFAULT (0),
    [Sets]                       INT              NULL,
    [Reps]                       INT              NULL,
    [DurationSeconds]            INT              NULL,
    [RestSeconds]                INT              NULL,
    [Notes]                      NVARCHAR(500)    NULL,

    CONSTRAINT [PK_WorkoutTemplateExercises]
        PRIMARY KEY ([WorkoutTemplateExerciseId]),

    CONSTRAINT [FK_WorkoutTemplateExercises_Template]
        FOREIGN KEY ([WorkoutTemplateId]) REFERENCES [catalog].[WorkoutTemplates]([WorkoutTemplateId]) ON DELETE CASCADE,

    CONSTRAINT [FK_WorkoutTemplateExercises_Exercise]
        FOREIGN KEY ([ExerciseId]) REFERENCES [catalog].[Exercises]([ExerciseId])
);
GO

CREATE INDEX [IX_WorkoutTemplateExercises_TemplateId]
    ON [catalog].[WorkoutTemplateExercises] ([WorkoutTemplateId]);
GO
