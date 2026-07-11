namespace ReqNest.Core.Reports;

public sealed record ReportPdfContent(
    string CompanyName,
    string? CompanyContact,
    string ReportTitle,
    string GeneratedLabel,
    string GeneratedValue,
    string TimeZoneLabel,
    string TimeZoneValue,
    string RequestedByLabel,
    string RequestedByValue,
    string FiltersLabel,
    string FiltersValue,
    string DefinitionsLabel,
    string Footer,
    IReadOnlyCollection<string> Definitions,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string[]> Rows,
    byte[]? LogoBytes = null);

public interface IReportPdfGenerator
{
    byte[] Generate(ReportPdfContent content);
}
