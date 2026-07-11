using ReqNest.Core.Common;
using ReqNest.Core.Tenancy;

namespace ReqNest.Core.Workflows;

public enum WorkflowStatusCategory
{
    ToDo,
    InProgress,
    Done,
}

public sealed class Workflow : Entity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Guid? ProjectId { get; set; }

    public Project? Project { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<WorkflowStatus> Statuses { get; set; } = [];

    public ICollection<WorkflowTransition> Transitions { get; set; } = [];
}

public sealed class WorkflowStatus : Entity
{
    public Guid TenantId { get; set; }

    public Guid WorkflowId { get; set; }

    public Workflow Workflow { get; set; } = null!;

    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;


    public WorkflowStatusCategory Category { get; set; }

    public int Order { get; set; }

    public string Color { get; set; } = "#64748b";

    public bool IsInitial { get; set; }

    public bool IsTerminal { get; set; }
}

public sealed class WorkflowTransition : Entity
{
    public Guid TenantId { get; set; }

    public Guid WorkflowId { get; set; }

    public Workflow Workflow { get; set; } = null!;

    public Guid FromStatusId { get; set; }

    public WorkflowStatus FromStatus { get; set; } = null!;

    public Guid ToStatusId { get; set; }

    public WorkflowStatus ToStatus { get; set; } = null!;

    public string? Name { get; set; }


    public bool CommentRequired { get; set; }
}
