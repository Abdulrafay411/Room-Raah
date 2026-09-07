using AutoMapper;
using RoomRaah.Application.DTOs;
using RoomRaah.Domain.Entities;

namespace RoomRaah.Application.Mappings;

/// <summary>
/// Conversations and messages. Two fields here are deliberately not mapped, and both
/// for the same reason: they depend on who is reading. A mapping cannot know that, and
/// one that quietly produced a default would be a lie nobody could see — the same
/// reason <c>SavedAt</c> and <c>DistanceKm</c> are ignored in the profiles next door.
/// </summary>
public class MessagingProfile : Profile
{
    public MessagingProfile()
    {
        CreateMap<Conversation, ConversationDto>()
            .ForMember(d => d.PropertyTitle, opt => opt.MapFrom(s => s.Property.Title))
            .ForMember(d => d.OtherParticipantDisplayName, opt => opt.Ignore())
            .ForMember(d => d.UnreadCount, opt => opt.Ignore());

        CreateMap<Message, MessageDto>()
            .ForMember(d => d.SenderDisplayName, opt => opt.MapFrom(s => s.Sender.FullName))
            .ForMember(d => d.IsMine, opt => opt.Ignore());
    }
}
