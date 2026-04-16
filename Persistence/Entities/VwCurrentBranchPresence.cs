using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GymSaaS.Persistence.Entities;

[Keyless]
public partial class VwCurrentBranchPresence
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid MemberId { get; set; }

    [Precision(0)]
    public DateTime? LastCheckInAtUtc { get; set; }

    [Precision(0)]
    public DateTime? PresenceUntilUtc { get; set; }
}
