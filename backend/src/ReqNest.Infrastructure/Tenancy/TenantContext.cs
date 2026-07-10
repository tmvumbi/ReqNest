using ReqNest.Core.Tenancy;

namespace ReqNest.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }

    public Guid? UserId { get; set; }
}
