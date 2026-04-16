using GymSaaS.Persistence;
using GymSaaS.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Services
{
    /// <summary>
    /// Handles generation and management of branch QR codes.
    /// QR value format: BRANCH:{BranchId}:V{version}:{timestamp}
    /// This value is what gets encoded into the actual QR image.
    /// The mobile app sends it to the attendance API which validates it.
    /// </summary>
    public class QrCodeService
    {
        private readonly GymDbContext _db;

        public QrCodeService(GymDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Generates a new QR code for a branch.
        /// Deactivates any existing active QR for that branch first.
        /// </summary>
        public async Task<BranchQrcode> GenerateForBranchAsync(
            Guid tenantId,
            Guid branchId,
            Guid createdByUserId,
            int versionNo = 1)
        {
            // Deactivate all existing active QR codes for this branch
            var existing = await _db.BranchQrcodes
                .Where(q => q.BranchId == branchId && q.IsActive)
                .ToListAsync();

            foreach (var old in existing)
            {
                old.IsActive       = false;
                old.EffectiveToUtc = DateTime.UtcNow;
            }

            // Build the QR value — this is what gets encoded in the QR image
            // Format: BRANCH:{branchId}:V{version}:{uniqueToken}
            var uniqueToken  = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            var qrValue      = $"BRANCH:{branchId}:V{versionNo}:{uniqueToken}";

            // Build a simple signed payload (HMAC-style concat for now)
            // In production replace this with a proper HMAC-SHA256 signature
            var signedPayload = $"{qrValue}|TENANT:{tenantId}|TS:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            var qr = new BranchQrcode
            {
                BranchQrcodeId    = Guid.NewGuid(),
                TenantId          = tenantId,
                BranchId          = branchId,
                QrCodeValue       = qrValue,
                SignedPayload      = signedPayload,
                VersionNo         = versionNo,
                IsActive          = true,
                EffectiveFromUtc  = DateTime.UtcNow,
                EffectiveToUtc    = null,
                CreatedAtUtc      = DateTime.UtcNow,
                CreatedByUserId   = createdByUserId,
            };

            _db.BranchQrcodes.Add(qr);
            await _db.SaveChangesAsync();

            return qr;
        }

        /// <summary>
        /// Gets the current active QR for a branch, or null if none.
        /// </summary>
        public async Task<BranchQrcode?> GetActiveQrAsync(Guid branchId)
        {
            return await _db.BranchQrcodes
                .Where(q => q.BranchId == branchId && q.IsActive)
                .OrderByDescending(q => q.VersionNo)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Regenerates the QR for a branch (increments version, deactivates old).
        /// </summary>
        public async Task<BranchQrcode> RegenerateAsync(
            Guid tenantId,
            Guid branchId,
            Guid createdByUserId)
        {
            var currentVersion = await _db.BranchQrcodes
                .Where(q => q.BranchId == branchId)
                .MaxAsync(q => (int?)q.VersionNo) ?? 0;

            return await GenerateForBranchAsync(
                tenantId,
                branchId,
                createdByUserId,
                currentVersion + 1);
        }

        /// <summary>
        /// Validates a scanned QR value — returns the BranchId if valid, null if not.
        /// Called by the mobile API when a member scans.
        /// </summary>
        public async Task<Guid?> ValidateQrValueAsync(string qrValue)
        {
            var qr = await _db.BranchQrcodes
                .FirstOrDefaultAsync(q => q.QrCodeValue == qrValue && q.IsActive);

            if (qr == null) return null;
            if (qr.EffectiveToUtc.HasValue && qr.EffectiveToUtc < DateTime.UtcNow) return null;

            return qr.BranchId;
        }
    }
}
