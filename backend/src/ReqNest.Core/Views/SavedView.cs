using ReqNest.Core.Common;

namespace ReqNest.Core.Views;

public sealed class SavedView : Entity
{
    public Guid TenantId { get; set; }

    public Guid OwnerUserId { get; set; }

    public Guid? ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string FiltersJson { get; set; } = "{}";

    public string SortJson { get; set; } = "{}";

    public string ColumnsJson { get; set; } = "[]";

    public string? GroupBy { get; set; }
}
