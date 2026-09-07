namespace RoomRaah.Application.Messaging;

/// <summary>
/// Every refusal this area can give. Kept in one place for the same reason the seeker
/// messages are: none of these sentences may name the other participant, and that is
/// far easier to keep true when they are all readable on one screen.
/// </summary>
public static class MessagingMessages
{
    public const string NotAParticipant =
        "You are not a participant in this conversation.";

    public const string ConversationNotFound =
        "That conversation does not exist.";

    /// <summary>
    /// Rule 100. An owner has nobody to address — there is no directory of seekers in
    /// this product and rule 33 would not permit one — so the listing is the
    /// introduction and it points one way.
    /// </summary>
    public const string OnlySeekersMayStart =
        "Only a seeker may open a conversation about a listing.";
}
