using AngleSharp.Html.Parser;
using Ganss.Xss;
using ReqNest.Core.Content;

namespace ReqNest.Infrastructure.Content;

public sealed class RichContentSanitizer : IRichContentSanitizer
{
    private static readonly string[] AllowedTags =
    [
        "p", "br", "strong", "b", "em", "i", "s", "h2", "h3", "h4", "ol", "ul", "li",
        "code", "pre", "blockquote", "a", "table", "thead", "tbody", "tr", "th", "td",
    ];

    private readonly HtmlSanitizer sanitizer = CreateSanitizer();
    private readonly HtmlParser parser = new();

    public SanitizedContent Sanitize(string content)
    {
        var html = sanitizer.Sanitize(content ?? string.Empty).Trim();
        var document = parser.ParseDocument($"<body>{html}</body>");
        var plainText = string.Join(
            ' ',
            (document.Body?.TextContent ?? string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return new SanitizedContent(html, plainText);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in AllowedTags)
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");
        return sanitizer;
    }
}
