using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant;

namespace ServiceDeskLite.Tests.Api.Assistant;

public sealed class RememberToolInputTests
{
    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public void TryParseInput_WithContentOnly_DefaultsKindToFact()
    {
        var input = Json("""{ "content": "Works in the Berlin office" }""");

        var ok = RememberTool.TryParseInput(input, out var content, out var kind, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        content.Should().Be("Works in the Berlin office");
        kind.Should().Be("fact");
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("preference")]
    [InlineData("fact")]
    public void TryParseInput_WithValidKind_UsesIt(string kind)
    {
        var input = Json($$"""{ "content": "x", "kind": "{{kind}}" }""");

        var ok = RememberTool.TryParseInput(input, out _, out var parsedKind, out _);

        ok.Should().BeTrue();
        parsedKind.Should().Be(kind);
    }

    [Fact]
    public void TryParseInput_TrimsContent()
    {
        var input = Json("""{ "content": "  reach me by email  " }""");

        RememberTool.TryParseInput(input, out var content, out _, out _);

        content.Should().Be("reach me by email");
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "content": "" }""")]
    [InlineData("""{ "content": "   " }""")]
    [InlineData("""{ "content": 42 }""")]
    public void TryParseInput_WithMissingOrInvalidContent_Fails(string json)
    {
        var ok = RememberTool.TryParseInput(Json(json), out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("content");
    }

    [Fact]
    public void TryParseInput_WithUnknownKind_Fails()
    {
        var input = Json("""{ "content": "x", "kind": "secret" }""");

        var ok = RememberTool.TryParseInput(input, out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("kind");
    }

    [Fact]
    public void TryParseInput_WithOverlongContent_Fails()
    {
        var longContent = new string('a', 501);
        var input = Json($$"""{ "content": "{{longContent}}" }""");

        var ok = RememberTool.TryParseInput(input, out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("content");
    }

    [Fact]
    public void TryParseInput_WithNonObjectInput_Fails()
    {
        var ok = RememberTool.TryParseInput(Json("\"just a string\""), out _, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("JSON object");
    }
}
