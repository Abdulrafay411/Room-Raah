using MediatR;
using RoomRaah.Application.DTOs;

namespace RoomRaah.Application.Features.Inspections.Queries;

/// <summary>
/// Rule 136. The listings whose owner has asked for an inspection that has not happened
/// yet. The due worklist (rule 114) only ever holds listings that already carry the
/// badge, so without this a first inspection an owner asked for had nowhere to appear.
/// </summary>
public class GetRequestedInspectionsQuery : IRequest<PagedResult<RequestedInspectionDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}
