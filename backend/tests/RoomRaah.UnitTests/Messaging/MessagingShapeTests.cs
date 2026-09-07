using RoomRaah.Application.DTOs;
using Shouldly;

namespace RoomRaah.UnitTests.Messaging;

/// <summary>
/// What the messaging shapes are allowed to carry.
///
/// This is rule 33 and rule 93 in the one place they are most likely to be broken by
/// accident: a conversation is two named people talking, so the temptation to put an
/// id or an email on the shape "just for the client" is real, and any of them would
/// hand one user something to reach the other with off the site.
/// </summary>
public class MessagingShapeTests
{
    [Fact]
    public void A_conversation_names_the_other_participant_and_identifies_neither()
    {
        var names = typeof(ConversationDto).GetProperties().Select(p => p.Name).ToArray();

        names.ShouldContain(nameof(ConversationDto.OtherParticipantDisplayName));

        // No seeker id, no owner id, and no field that names one side rather than
        // "the other one" — a shape with an OwnerDisplayName would be filled in for
        // the owner reading their own inbox on the day somebody forgot to check.
        names.ShouldNotContain("SeekerId");
        names.ShouldNotContain("OwnerId");
        names.ShouldNotContain("OwnerDisplayName");
        names.ShouldNotContain("SeekerDisplayName");
        names.ShouldNotContain("OwnerEmail");
        names.ShouldNotContain("Email");
        names.ShouldNotContain("PhoneNumber");
    }

    [Fact]
    public void A_message_carries_a_name_and_a_side_but_no_identity()
    {
        var names = typeof(MessageDto).GetProperties().Select(p => p.Name).ToArray();

        names.ShouldContain(nameof(MessageDto.SenderDisplayName));
        names.ShouldContain(nameof(MessageDto.IsMine));

        names.ShouldNotContain("SenderId");
        names.ShouldNotContain("SenderEmail");
        names.ShouldNotContain("Email");
        names.ShouldNotContain("PhoneNumber");
    }
}
