using AngleSharp.Html.Parser;
using Ganss.Xss;
using ReqNest.Core.Content;

namespace ReqNest.Infrastructure.Content;

public sealed class RichContentSanitizer : IRichContentSanitizer
{
    private static readonly string[] AllowedTags =
    [
        "p", "br", "strong", "b", "em", "i", "u", "s", "sub", "sup", "span",
        "h1", "h2", "h3", "h4", "ol", "ul", "li",
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
        var mentionUserIds = document
            .QuerySelectorAll("a.mention[data-user-id]")
            .Select(anchor => Guid.TryParse(anchor.GetAttribute("data-user-id"), out var userId) ? userId : Guid.Empty)
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();
        return new SanitizedContent(html, plainText, mentionUserIds);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer
        {
            // Unknown tags are unwrapped instead of deleted so the text survives
            // even when an editor emits markup outside the allow-list.
            KeepChildNodes = true,
        };
        sanitizer.AllowedTags.Clear();
        foreach (var tag in AllowedTags)
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedAttributes.Add("style");
        sanitizer.AllowedAttributes.Add("data-list");
        sanitizer.AllowedAttributes.Add("data-user-id");
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedCssProperties.Add("color");
        sanitizer.AllowedCssProperties.Add("background-color");
        sanitizer.AllowedCssProperties.Add("text-align");
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");
        return sanitizer;
    }
}
