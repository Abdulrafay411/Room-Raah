using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Properties.Commands;

public class UploadPropertyPhotoCommand : IRequest<PropertyPhotoDto>
{
    public int PropertyId { get; set; }

    /// <summary>Set from the bearer token by the controller.</summary>
    public int OwnerId { get; set; }

    public string FileName { get; set; } = string.Empty;

    /// <summary>What the client says the file is. The bytes are what decide.</summary>
    public string ContentType { get; set; } = string.Empty;

    public long Length { get; set; }

    public Stream Content { get; set; } = Stream.Null;
}
