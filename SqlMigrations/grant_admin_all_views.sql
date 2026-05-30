-- ================================================================
-- Grant Admin role access to ALL view permissions
-- Safe to run multiple times (NOT EXISTS guard on inserts).
-- After running: log out and back in to pick up the new claims.
-- ================================================================

DECLARE @AdminRoleId UNIQUEIDENTIFIER = (
    SELECT TOP 1 RoleId
    FROM identityx.Roles
    WHERE RoleName = 'Admin'
      AND IsDeleted = 0
);

IF @AdminRoleId IS NULL
BEGIN
    RAISERROR ('Admin role not found. Aborting.', 16, 1);
    RETURN;
END

PRINT 'Admin RoleId: ' + CAST(@AdminRoleId AS NVARCHAR(50));

-- ── Step 1: Ensure commonly-missed view permissions exist ─────────
-- The app syncs ViewPermissions automatically when the Roles page
-- is visited, but these folders may not have been synced yet.
MERGE identityx.ViewPermissions AS target
USING (VALUES
    ('views.attendance',     'Attendance',     'Views', '/Attendance'),
    ('views.notifications',  'Notifications',  'Views', '/Notifications'),
    ('views.auditlogs',      'AuditLogs',      'Views', '/AuditLogs'),
    ('views.settings',       'Settings',       'Views', '/Settings')
) AS src (PermissionCode, DisplayName, GroupName, Route)
ON target.PermissionCode = src.PermissionCode
WHEN NOT MATCHED THEN
    INSERT (ViewPermissionId, PermissionCode, DisplayName, GroupName, Route, SortOrder, IsActive, CreatedAtUtc)
    VALUES (NEWID(), src.PermissionCode, src.DisplayName, src.GroupName, src.Route, 0, 1, SYSUTCDATETIME())
WHEN MATCHED AND target.IsActive = 0 THEN
    UPDATE SET target.IsActive = 1;

PRINT 'View permissions synced.';

-- ── Step 2: Grant all active view permissions to Admin ────────────
INSERT INTO identityx.RoleViewPermissions
    (RoleViewPermissionId, RoleId, ViewPermissionId, CreatedAtUtc)
SELECT
    NEWID(),
    @AdminRoleId,
    vp.ViewPermissionId,
    SYSUTCDATETIME()
FROM identityx.ViewPermissions vp
WHERE vp.IsActive = 1
  AND NOT EXISTS (
      SELECT 1
      FROM identityx.RoleViewPermissions rvp
      WHERE rvp.RoleId           = @AdminRoleId
        AND rvp.ViewPermissionId = vp.ViewPermissionId
  );

PRINT 'Permissions granted to Admin.';

-- ── Step 3: Show what Admin can now access ────────────────────────
SELECT
    vp.PermissionCode,
    vp.DisplayName
FROM identityx.RoleViewPermissions rvp
JOIN identityx.ViewPermissions vp
    ON vp.ViewPermissionId = rvp.ViewPermissionId
WHERE rvp.RoleId  = @AdminRoleId
  AND vp.IsActive = 1
ORDER BY vp.PermissionCode;

PRINT '================================================================';
PRINT 'Done. Log out and back in to activate the updated permissions.';
PRINT '================================================================';
