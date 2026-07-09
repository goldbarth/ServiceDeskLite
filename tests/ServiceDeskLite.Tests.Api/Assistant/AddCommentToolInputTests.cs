using System.Text.Json;

using FluentAssertions;

using ServiceDeskLite.Api.Assistant;
using ServiceDeskLite.Domain.Audit;
using ServiceDeskLite.Domain.Tickets;

namespace ServiceDeskLite.Tests.Api.Assistant;

/// <summary>
/// Tool input is model output, and model output is untrusted. What the parser rejects, and what it
/// tells the model when it does, is the whole contract before a handler is ever reached.
/// </summary>
public sealed class AddCommentToolInputTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);
    private const string TicketId = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement;

    [Fact]
    public void A_valid_comment_parses_into_a_command()
    {
        var ok = AddCommentTool.TryParseInput(
            Json($$"""{"ticketId":"{{TicketId}}","content":"Which VPN client version are you on?"}"""),
            Now, out var command, out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        command!.TicketId.Should().Be(new TicketId(Guid.Parse(TicketId)));
        command.Content.Should().Be("Which VPN client version are you on?");
        command.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void The_author_defaults_to_the_assistant_and_the_worker_overrides_it()
    {
        // The audit trail has to say which of the two wrote this, and so does the comment header.
        AddCommentTool.TryParseInput(
            Json($$"""{"ticketId":"{{TicketId}}","content":"hi"}"""), Now, out var interactive, out _);

        AddCommentTool.TryParseInput(
            Json($$"""{"ticketId":"{{TicketId}}","content":"hi"}"""), Now, out var autonomous, out _,
            AuditActors.AiWorker);

        interactive!.Author.Should().Be(AuditActors.AiAssistant);
        autonomous!.Author.Should().Be(AuditActors.AiWorker);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"a string\"")]
    public void Input_that_is_not_an_object_is_rejected(string raw)
    {
        AddCommentTool.TryParseInput(Json(raw), Now, out _, out var error).Should().BeFalse();
        error.Should().Contain("JSON object");
    }

    [Fact]
    public void A_missing_ticket_id_names_where_the_model_can_find_one()
    {
        AddCommentTool.TryParseInput(
            Json("""{"content":"hello"}"""), Now, out _, out var error).Should().BeFalse();

        error.Should().Contain("ticketId");
        error.Should().Contain("search_tickets", "the model has to know how to recover");
    }

    [Fact]
    public void A_ticket_id_that_is_not_a_uuid_is_rejected()
    {
        AddCommentTool.TryParseInput(
            Json("""{"ticketId":"the printer one","content":"hello"}"""), Now, out _, out var error)
            .Should().BeFalse();

        error.Should().Contain("ticketId");
    }

    [Theory]
    [InlineData($$"""{"ticketId":"{{TicketId}}"}""")]
    [InlineData($$"""{"ticketId":"{{TicketId}}","content":""}""")]
    [InlineData($$"""{"ticketId":"{{TicketId}}","content":"   "}""")]
    [InlineData($$"""{"ticketId":"{{TicketId}}","content":42}""")]
    public void Empty_or_missing_content_is_rejected(string raw)
    {
        AddCommentTool.TryParseInput(Json(raw), Now, out _, out var error).Should().BeFalse();
        error.Should().Contain("content");
    }

    [Fact]
    public void Content_beyond_the_domain_limit_is_refused_with_the_limit_as_a_number()
    {
        var tooLong = new string('x', Comment.MaxContentLength + 1);

        AddCommentTool.TryParseInput(
            Json($$"""{"ticketId":"{{TicketId}}","content":"{{tooLong}}"}"""), Now, out _, out var error)
            .Should().BeFalse();

        error.Should().Contain(Comment.MaxContentLength.ToString(),
            "a field-error dictionary is harder for the model to act on than a number and an instruction");
        error.Should().Contain("Summarise");
    }
}
