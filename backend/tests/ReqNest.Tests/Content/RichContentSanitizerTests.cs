using ReqNest.Infrastructure.Content;

namespace ReqNest.Tests.Content;

public sealed class RichContentSanitizerTests
{
    [Fact]
    public void Sanitize_extracts_mention_user_ids_and_keeps_mention_markup()
    {
        var sanitizer = new RichContentSanitizer();
        var userId = Guid.NewGuid();
        var html =
            $"<p>Ping <a class=\"mention\" data-user-id=\"{userId}\">@Jane Doe</a> twice " +
            $"<a class=\"mention\" data-user-id=\"{userId}\">@Jane Doe</a> and " +
            "<a class=\"mention\" data-user-id=\"not-a-guid\">@broken</a></p>";

        var content = sanitizer.Sanitize(html);

        Assert.Equal([userId], content.MentionUserIds);
        Assert.Contains($"data-user-id=\"{userId}\"", content.Html, StringComparison.Ordinal);
        Assert.Contains("class=\"mention\"", content.Html, StringComparison.Ordinal);
        Assert.Contains("@Jane Doe", content.PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_returns_no_mentions_for_plain_links()
    {
        var sanitizer = new RichContentSanitizer();

        var content = sanitizer.Sanitize("<p>See <a href=\"https://example.test\">docs</a></p>");

        Assert.Empty(content.MentionUserIds);
    }
}
