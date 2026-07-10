namespace ReqNest.Core.Reports;

public sealed record ReportPdfContent(
    string CompanyName,
    string ReportTitle,
    string GeneratedLabel,
    string TimeZoneLabel,
    string RequestedByLabel,
    string FiltersLabel,
    string DefinitionsLabel,
    string Footer,
    IReadOnlyCollection<string> Definitions,
    IReadOnlyCollection<string> TableLines,
    byte[]? LogoBytes = null);

public interface IReportPdfGenerator
{
    byte[] Generate(ReportPdfContent content);
}
