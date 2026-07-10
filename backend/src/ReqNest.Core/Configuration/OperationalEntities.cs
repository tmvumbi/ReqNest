using ReqNest.Core.Common;

namespace ReqNest.Core.Configuration;

public enum CustomFieldKind
{
    Text,
    Number,
    Date,
    Boolean,
    Choice,
}

public sealed class TicketTypeDefinition : Entity
{
    public Guid TenantId { get; set; }

    public Guid? ProjectId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string LabelEnglish { get; set; } = string.Empty;

    public string LabelFrench { get; set; } = string.Empty;

    public int Order { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class TicketPriorityDefinition : Entity
{
    public Guid TenantId { get; set; }

    public Guid? ProjectId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string LabelEnglish { get; set; } = string.Empty;

    public string LabelFrench { get; set; } = string.Empty;

    public string Color { get; set; } = "#64748b";

    public int Weight { get; set; }

    public int Order { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class CustomFieldDefinition : Entity
{
    public Guid TenantId { get; set; }

    public Guid? ProjectId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string LabelEnglish { get; set; } = string.Empty;

    public string LabelFrench { get; set; } = string.Empty;

    public CustomFieldKind Kind { get; set; }

    public bool IsRequired { get; set; }

    public bool IsActive { get; set; } = true;

    public int Order { get; set; }

    public string OptionsJson { get; set; } = "[]";
}

public sealed class TicketCustomFieldValue : Entity
{
    public Guid TenantId { get; set; }

    public Guid TicketId { get; set; }

    public Guid DefinitionId { get; set; }

    public string ValueJson { get; set; } = "null";
}

public sealed class SlaPolicy : Entity
{
    public Guid TenantId { get; set; }

    public Guid? ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string TimeZone { get; set; } = "UTC";

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public int WorkingDaysMask { get; set; } = 62;

    public int BusinessDayStartMinutes { get; set; } = 540;

    public int BusinessDayEndMinutes { get; set; } = 1020;

    public int WarningMinutesBefore { get; set; } = 60;

    public string[] PauseStatusKeys { get; set; } = [];

    public ICollection<SlaPriorityTarget> Targets { get; set; } = [];

    public ICollection<SlaHoliday> Holidays { get; set; } = [];
}

public sealed class SlaPriorityTarget : Entity
{
    public Guid TenantId { get; set; }

    public Guid SlaPolicyId { get; set; }

    public SlaPolicy SlaPolicy { get; set; } = null!;

    public string PriorityKey { get; set; } = string.Empty;

    public int FirstResponseMinutes { get; set; }

    public int ResolutionMinutes { get; set; }
}

public sealed class SlaHoliday : Entity
{
    public Guid TenantId { get; set; }

    public Guid SlaPolicyId { get; set; }

    public SlaPolicy SlaPolicy { get; set; } = null!;

    public DateOnly Date { get; set; }

    public string Name { get; set; } = string.Empty;
}
