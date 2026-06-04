-- ================================================================
-- Migration: Partnerships + MemberPartnerships
-- ================================================================

-- Partnerships table
CREATE TABLE [membership].[Partnerships] (
    [PartnershipId]       UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Partnerships_Id] DEFAULT (newsequentialid()),
    [TenantId]            UNIQUEIDENTIFIER NOT NULL,
    [Name]                NVARCHAR(200)    NOT NULL,
    [Description]         NVARCHAR(1000)   NULL,
    [LogoImageUrl]        NVARCHAR(1000)   NULL,
    [DiscountPercentage]  DECIMAL(5,2)     NULL,
    [IsActive]            BIT              NOT NULL CONSTRAINT [DF_Partnerships_IsActive] DEFAULT (1),
    [CreatedAtUtc]        DATETIME2(0)     NOT NULL CONSTRAINT [DF_Partnerships_CreatedAt] DEFAULT (sysutcdatetime()),
    [CreatedByUserId]     UNIQUEIDENTIFIER NULL,
    [UpdatedAtUtc]        DATETIME2(0)     NULL,
    [UpdatedByUserId]     UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_Partnerships] PRIMARY KEY ([PartnershipId])
);
GO

CREATE INDEX [IX_Partnerships_TenantId] ON [membership].[Partnerships] ([TenantId]);
GO

-- MemberPartnerships junction table
CREATE TABLE [membership].[MemberPartnerships] (
    [MemberPartnershipId] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_MemberPartnerships_Id] DEFAULT (newsequentialid()),
    [TenantId]            UNIQUEIDENTIFIER NOT NULL,
    [MemberId]            UNIQUEIDENTIFIER NOT NULL,
    [PartnershipId]       UNIQUEIDENTIFIER NOT NULL,
    [CreatedAtUtc]        DATETIME2(0)     NOT NULL CONSTRAINT [DF_MemberPartnerships_CreatedAt] DEFAULT (sysutcdatetime()),
    [CreatedByUserId]     UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_MemberPartnerships] PRIMARY KEY ([MemberPartnershipId]),
    CONSTRAINT [UQ_MemberPartnerships_Member_Partnership] UNIQUE ([MemberId], [PartnershipId]),
    CONSTRAINT [FK_MemberPartnerships_Members]
        FOREIGN KEY ([MemberId]) REFERENCES [membership].[Members] ([MemberId]) ON DELETE CASCADE,
    CONSTRAINT [FK_MemberPartnerships_Partnerships]
        FOREIGN KEY ([PartnershipId]) REFERENCES [membership].[Partnerships] ([PartnershipId]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_MemberPartnerships_MemberId]     ON [membership].[MemberPartnerships] ([MemberId]);
CREATE INDEX [IX_MemberPartnerships_PartnershipId] ON [membership].[MemberPartnerships] ([PartnershipId]);
CREATE INDEX [IX_MemberPartnerships_TenantId]      ON [membership].[MemberPartnerships] ([TenantId]);
GO
