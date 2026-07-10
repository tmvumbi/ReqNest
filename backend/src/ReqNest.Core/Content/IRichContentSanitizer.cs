namespace ReqNest.Core.Content;

public sealed record SanitizedContent(string Html, string PlainText);

public interface IRichContentSanitizer
{
    SanitizedContent Sanitize(string content);
}
