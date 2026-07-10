namespace ReqNest.Core.Tenancy;

public interface ITenantContext
{
    Guid? TenantId { get; set; }

    Guid? UserId { get; set; }
}
