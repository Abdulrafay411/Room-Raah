using FluentValidation.TestHelper;
using RoomRaah.Application.Features.Messaging.Commands;
using RoomRaah.Application.Features.Messaging.Queries;
using Shouldly;

namespace RoomRaah.UnitTests.Messaging;

public class MessagingValidatorTests
{
    private readonly StartConversationCommandValidator _start = new();
    private readonly SendMessageCommandValidator _send = new();
    private readonly GetConversationsQueryValidator _inbox = new();
    private readonly GetMessagesQueryValidator _thread = new();

    [Fact]
    public void A_conversation_needs_a_listing()
    {
        _start.TestValidate(new StartConversationCommand { PropertyId = 0 })
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_conversation_on_a_real_listing_is_accepted()
    {
        _start.TestValidate(new StartConversationCommand { PropertyId = 3 })
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void A_message_needs_a_body()
    {
        _send.TestValidate(new SendMessageCommand { Body = string.Empty })
            .IsValid.ShouldBeFalse();
    }

    /// <summary>
    /// Rule 45's floor is one character, and a body of four spaces has a length of
    /// four. NotEmpty is what makes "not empty" mean what it says.
    /// </summary>
    [Fact]
    public void A_message_of_nothing_but_spaces_is_empty()
    {
        _send.TestValidate(new SendMessageCommand { Body = "    " })
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_message_at_the_limit_is_accepted()
    {
        _send.TestValidate(new SendMessageCommand
        {
            Body = new string('a', SendMessageCommandValidator.MaximumBodyLength)
        }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void A_message_past_the_limit_is_refused()
    {
        _send.TestValidate(new SendMessageCommand
        {
            Body = new string('a', SendMessageCommandValidator.MaximumBodyLength + 1)
        }).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public void An_impossible_page_is_refused(int page, int pageSize)
    {
        _inbox.TestValidate(new GetConversationsQuery { Page = page, PageSize = pageSize })
            .IsValid.ShouldBeFalse();

        _thread.TestValidate(new GetMessagesQuery { Page = page, PageSize = pageSize })
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void The_defaults_are_a_valid_page()
    {
        _inbox.TestValidate(new GetConversationsQuery()).IsValid.ShouldBeTrue();
        _thread.TestValidate(new GetMessagesQuery()).IsValid.ShouldBeTrue();
    }
}
