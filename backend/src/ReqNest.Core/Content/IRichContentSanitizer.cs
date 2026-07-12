namespace ReqNest.Core.Content;

public sealed record SanitizedContent(string Html, string PlainText, IReadOnlyCollection<Guid> MentionUserIds);

public interface IRichContentSanitizer
{
    SanitizedContent Sanitize(string content);
}
