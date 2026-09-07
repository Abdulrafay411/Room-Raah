using RoomRaah.Application.Messaging;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Messaging;

/// <summary>
/// Rule 102, asserted where it is decided rather than through the four endpoints that
/// ask it. Four endpoints asking the same question is four chances to ask it slightly
/// differently, and this is the answer all four of them use.
/// </summary>
public class ConversationAccessTests
{
    private const int OwnerId = 1;
    private const int SeekerId = 2;
    private const int Stranger = 99;

    [Theory]
    [InlineData(SeekerId, true)]
    [InlineData(OwnerId, true)]
    [InlineData(Stranger, false)]
    public void Only_the_two_participants_are_participants(int userId, bool expected)
    {
        var conversation = TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId);

        ConversationAccess.IsParticipant(conversation, userId).ShouldBe(expected);
    }

    [Fact]
    public void A_stranger_is_refused_rather_than_told_what_is_in_the_thread()
    {
        var conversation = TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId);

        var error = Should.Throw<UnauthorizedAccessException>(() =>
            ConversationAccess.RequireParticipant(conversation, Stranger));

        error.Message.ShouldBe(MessagingMessages.NotAParticipant);
    }

    [Theory]
    [InlineData(SeekerId, OwnerId)]
    [InlineData(OwnerId, SeekerId)]
    public void The_other_participant_is_whichever_one_the_caller_is_not(int viewer, int expected)
    {
        var conversation = TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId);

        ConversationAccess.Other(conversation, viewer).ShouldBe(expected);
    }

    /// <summary>
    /// Rule 101 and rule 33 in one assertion: whichever side reads the thread, the
    /// other person is a name. There is nothing here to reach them with off the site,
    /// which is the entire reason the conversation is on the site.
    /// </summary>
    [Fact]
    public void Each_side_sees_the_other_as_a_display_name()
    {
        var conversation = TestData.Conversation(seekerId: SeekerId, ownerId: OwnerId);
        conversation.Seeker.FullName = "Ayesha Khan";
        conversation.Owner.FullName = "Bilal Ahmed";

        ConversationAccess.OtherDisplayName(conversation, SeekerId).ShouldBe("Bilal Ahmed");
        ConversationAccess.OtherDisplayName(conversation, OwnerId).ShouldBe("Ayesha Khan");
    }
}
