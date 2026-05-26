-- Migration: Add membership.MemberInvitations table
-- Run this once against your database before deploying the new code.

CREATE TABLE [membership].[MemberInvitations] (
    [MemberInvitationId]  UNIQUEIDENTIFIER NOT NULL DEFAULT (NEWSEQUENTIALID()),
    [TenantId]            UNIQUEIDENTIFIER NOT NULL,
    [MemberId]            UNIQUEIDENTIFIER NOT NULL,
    [MemberPackageId]     UNIQUEIDENTIFIER NOT NULL,
    [GuestName]           NVARCHAR(200)    NOT NULL,
    [GuestPhone]          NVARCHAR(30)     NOT NULL,
    [InvitedMemberId]     UNIQUEIDENTIFIER NULL,
    [Status]              NVARCHAR(20)     NOT NULL DEFAULT ('PENDING'),
    [Notes]               NVARCHAR(500)    NULL,
    [CreatedAtUtc]        DATETIME2(0)     NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CreatedByUserId]     UNIQUEIDENTIFIER NULL,
    [UsedAtUtc]           DATETIME2(0)     NULL,

    CONSTRAINT [PK_MemberInvitations]
        PRIMARY KEY ([MemberInvitationId]),

    CONSTRAINT [FK_MemberInvitations_Tenants]
        FOREIGN KEY ([TenantId])
        REFERENCES [core].[Tenants] ([TenantId]),

    CONSTRAINT [FK_MemberInvitations_Members]
        FOREIGN KEY ([MemberId])
        REFERENCES [membership].[Members] ([MemberId]),

    CONSTRAINT [FK_MemberInvitations_MemberPackages]
        FOREIGN KEY ([MemberPackageId])
        REFERENCES [membership].[MemberPackages] ([MemberPackageId]),

    CONSTRAINT [FK_MemberInvitations_InvitedMember]
        FOREIGN KEY ([InvitedMemberId])
        REFERENCES [membership].[Members] ([MemberId]),

    CONSTRAINT [FK_MemberInvitations_CreatedBy]
        FOREIGN KEY ([CreatedByUserId])
        REFERENCES [identityx].[Users] ([UserId])
);
GO

CREATE INDEX [IX_MemberInvitations_MemberId]
    ON [membership].[MemberInvitations] ([MemberId]);
GO

CREATE INDEX [IX_MemberInvitations_MemberPackageId]
    ON [membership].[MemberInvitations] ([MemberPackageId]);
GO

CREATE INDEX [IX_MemberInvitations_TenantId_GuestPhone]
    ON [membership].[MemberInvitations] ([TenantId], [GuestPhone]);
GO
