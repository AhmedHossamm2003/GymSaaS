using System;
using System.Collections.Generic;
using GymSaaS.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence;

public partial class GymDbContext : DbContext
{
    public GymDbContext()
    {
    }

    public GymDbContext(DbContextOptions<GymDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AttendanceOverrideRequest> AttendanceOverrideRequests { get; set; }

    public virtual DbSet<AttendanceRecord> AttendanceRecords { get; set; }

    public virtual DbSet<AttendanceStatus> AttendanceStatuses { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<BranchAccessPolicyType> BranchAccessPolicyTypes { get; set; }

    public virtual DbSet<BranchQrcode> BranchQrcodes { get; set; }

    public virtual DbSet<Member> Members { get; set; }

    public virtual DbSet<MemberPackage> MemberPackages { get; set; }

    public virtual DbSet<MemberPackageAllowedBranch> MemberPackageAllowedBranches { get; set; }

    public virtual DbSet<MemberStatus> MemberStatuses { get; set; }

    public virtual DbSet<OverrideRequestStatus> OverrideRequestStatuses { get; set; }

    public virtual DbSet<PackageDefinition> PackageDefinitions { get; set; }

    public virtual DbSet<PackageType> PackageTypes { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Tenant> Tenants { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserBranch> UserBranches { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<VwActiveMemberPackage> VwActiveMemberPackages { get; set; }

    public virtual DbSet<VwCurrentBranchPresence> VwCurrentBranchPresences { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=tcp:main-server-db.database.windows.net,1433;Initial Catalog=GymDB;Persist Security Info=False;User ID=ServerAdmin;Password=Admin@2026;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AttendanceOverrideRequest>(entity =>
        {
            entity.Property(e => e.AttendanceOverrideRequestId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.RequestedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Branch).WithMany(p => p.AttendanceOverrideRequests)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AttendanceOverrideRequests_Branches");

            entity.HasOne(d => d.BranchQrcode).WithMany(p => p.AttendanceOverrideRequests).HasConstraintName("FK_AttendanceOverrideRequests_BranchQRCodes");

            entity.HasOne(d => d.DecisionByUser).WithMany(p => p.AttendanceOverrideRequests).HasConstraintName("FK_AttendanceOverrideRequests_DecisionBy");

            entity.HasOne(d => d.Member).WithMany(p => p.AttendanceOverrideRequests)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AttendanceOverrideRequests_Members");

            entity.HasOne(d => d.OverrideRequestStatus).WithMany(p => p.AttendanceOverrideRequests)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AttendanceOverrideRequests_Status");

            entity.HasOne(d => d.ResultingAttendanceRecord).WithMany(p => p.AttendanceOverrideRequests).HasConstraintName("FK_AttendanceOverrideRequests_ResultAttendance");

            entity.HasOne(d => d.Tenant).WithMany(p => p.AttendanceOverrideRequests)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AttendanceOverrideRequests_Tenants");
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.Property(e => e.AttendanceRecordId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CheckInAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.AttendanceStatus).WithMany(p => p.AttendanceRecords)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AttendanceRecords_Status");

            entity.HasOne(d => d.Branch).WithMany(p => p.AttendanceRecords)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AttendanceRecords_Branches");

            entity.HasOne(d => d.BranchQrcode).WithMany(p => p.AttendanceRecords).HasConstraintName("FK_AttendanceRecords_BranchQRCodes");

            entity.HasOne(d => d.Member).WithMany(p => p.AttendanceRecords)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AttendanceRecords_Members");

            entity.HasOne(d => d.MemberPackage).WithMany(p => p.AttendanceRecords).HasConstraintName("FK_AttendanceRecords_MemberPackages");

            entity.HasOne(d => d.OverrideRequest).WithMany(p => p.AttendanceRecords).HasConstraintName("FK_AttendanceRecords_OverrideRequest");

            entity.HasOne(d => d.ReceptionistDecisionUser).WithMany(p => p.AttendanceRecords).HasConstraintName("FK_AttendanceRecords_ReceptionistDecisionUser");

            entity.HasOne(d => d.Tenant).WithMany(p => p.AttendanceRecords)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AttendanceRecords_Tenants");
        });

        modelBuilder.Entity<AttendanceStatus>(entity =>
        {
            entity.Property(e => e.AttendanceStatusId).HasDefaultValueSql("(newsequentialid())");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.Property(e => e.BranchId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.CurrentQrVersion).HasDefaultValue(1);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MemberPresenceWindowMinutes).HasDefaultValue(120);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Branches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Branches_Tenants");
        });

        modelBuilder.Entity<BranchAccessPolicyType>(entity =>
        {
            entity.Property(e => e.BranchAccessPolicyTypeId).HasDefaultValueSql("(newsequentialid())");
        });

        modelBuilder.Entity<BranchQrcode>(entity =>
        {
            entity.Property(e => e.BranchQrcodeId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.EffectiveFromUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.VersionNo).HasDefaultValue(1);

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchQrcodes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BranchQRCodes_Branches");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.BranchQrcodes).HasConstraintName("FK_BranchQRCodes_CreatedBy");

            entity.HasOne(d => d.Tenant).WithMany(p => p.BranchQrcodes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BranchQRCodes_Tenants");
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.Property(e => e.MemberId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FullName).HasComputedColumnSql("(ltrim(rtrim(concat(isnull([FirstName],N''),N' ',isnull([LastName],N'')))))", false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.MemberCreatedByUsers).HasConstraintName("FK_Members_CreatedBy");

            entity.HasOne(d => d.DeletedByUser).WithMany(p => p.MemberDeletedByUsers).HasConstraintName("FK_Members_DeletedBy");

            entity.HasOne(d => d.HomeBranch).WithMany(p => p.Members)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Members_HomeBranch");

            entity.HasOne(d => d.MemberStatus).WithMany(p => p.Members)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Members_Status");

            entity.HasOne(d => d.Tenant).WithMany(p => p.Members)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Members_Tenants");

            entity.HasOne(d => d.UpdatedByUser).WithMany(p => p.MemberUpdatedByUsers).HasConstraintName("FK_Members_UpdatedBy");
        });

        modelBuilder.Entity<MemberPackage>(entity =>
        {
            entity.Property(e => e.MemberPackageId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.OpenGymDailyLimit).HasDefaultValue(1);

            entity.HasOne(d => d.BranchAccessPolicyType).WithMany(p => p.MemberPackages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MemberPackages_BranchPolicy");

            entity.HasOne(d => d.CancelledByUser).WithMany(p => p.MemberPackageCancelledByUsers).HasConstraintName("FK_MemberPackages_CancelledBy");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.MemberPackageCreatedByUsers).HasConstraintName("FK_MemberPackages_CreatedBy");

            entity.HasOne(d => d.HomeBranch).WithMany(p => p.MemberPackages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MemberPackages_HomeBranch");

            entity.HasOne(d => d.Member).WithMany(p => p.MemberPackages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MemberPackages_Members");

            entity.HasOne(d => d.PackageDefinition).WithMany(p => p.MemberPackages).HasConstraintName("FK_MemberPackages_PackageDefinitions");

            entity.HasOne(d => d.PackageType).WithMany(p => p.MemberPackages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MemberPackages_PackageTypes");

            entity.HasOne(d => d.RenewalOfMemberPackage).WithMany(p => p.InverseRenewalOfMemberPackage).HasConstraintName("FK_MemberPackages_RenewalOf");

            entity.HasOne(d => d.Tenant).WithMany(p => p.MemberPackages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MemberPackages_Tenants");

            entity.HasOne(d => d.UpdatedByUser).WithMany(p => p.MemberPackageUpdatedByUsers).HasConstraintName("FK_MemberPackages_UpdatedBy");
        });

        modelBuilder.Entity<MemberPackageAllowedBranch>(entity =>
        {
            entity.Property(e => e.AllowedBranchId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Branch).WithMany(p => p.MemberPackageAllowedBranches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MemberPackageAllowedBranches_Branches");

            entity.HasOne(d => d.MemberPackage).WithMany(p => p.MemberPackageAllowedBranches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MemberPackageAllowedBranches_MemberPackages");
        });

        modelBuilder.Entity<MemberStatus>(entity =>
        {
            entity.Property(e => e.MemberStatusId).HasDefaultValueSql("(newsequentialid())");
        });

        modelBuilder.Entity<OverrideRequestStatus>(entity =>
        {
            entity.Property(e => e.OverrideRequestStatusId).HasDefaultValueSql("(newsequentialid())");
        });

        modelBuilder.Entity<PackageDefinition>(entity =>
        {
            entity.Property(e => e.PackageDefinitionId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.AllowCarryOverSessions).HasDefaultValue(true);
            entity.Property(e => e.AllowCustomOverrideDuringAssignment).HasDefaultValue(true);
            entity.Property(e => e.AllowQueuedRenewal).HasDefaultValue(true);
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.OpenGymDailyLimit).HasDefaultValue(1);

            entity.HasOne(d => d.BranchAccessPolicyType).WithMany(p => p.PackageDefinitions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PackageDefinitions_BranchPolicy");

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.PackageDefinitionCreatedByUsers).HasConstraintName("FK_PackageDefinitions_CreatedBy");

            entity.HasOne(d => d.PackageType).WithMany(p => p.PackageDefinitions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PackageDefinitions_PackageTypes");

            entity.HasOne(d => d.Tenant).WithMany(p => p.PackageDefinitions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PackageDefinitions_Tenants");

            entity.HasOne(d => d.UpdatedByUser).WithMany(p => p.PackageDefinitionUpdatedByUsers).HasConstraintName("FK_PackageDefinitions_UpdatedBy");
        });

        modelBuilder.Entity<PackageType>(entity =>
        {
            entity.Property(e => e.PackageTypeId).HasDefaultValueSql("(newsequentialid())");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(e => e.RoleId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.Property(e => e.TenantId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.DefaultLanguage).HasDefaultValue("en");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TimeZoneId).HasDefaultValue("Africa/Cairo");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(e => e.UserId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.CreatedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.FullName).HasComputedColumnSql("(ltrim(rtrim(concat(isnull([FirstName],N''),N' ',isnull([LastName],N'')))))", false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Tenant).WithMany(p => p.Users)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Tenants");
        });

        modelBuilder.Entity<UserBranch>(entity =>
        {
            entity.Property(e => e.UserBranchId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.AssignedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Branch).WithMany(p => p.UserBranches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserBranches_Branches");

            entity.HasOne(d => d.User).WithMany(p => p.UserBranches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserBranches_Users");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.Property(e => e.UserRoleId).HasDefaultValueSql("(newsequentialid())");
            entity.Property(e => e.AssignedAtUtc).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRoles_Roles");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRoles_Users");
        });

        modelBuilder.Entity<VwActiveMemberPackage>(entity =>
        {
            entity.ToView("vw_ActiveMemberPackages", "membership");
        });

        modelBuilder.Entity<VwCurrentBranchPresence>(entity =>
        {
            entity.ToView("vw_CurrentBranchPresence", "attendance");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
