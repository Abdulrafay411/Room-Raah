using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Visits.Queries;

namespace RoomRaah.API.Controllers;

/// <summary>
/// The requests waiting on an owner's own listings. A separate controller because
/// the route sits under <c>/owner</c> with the rest of the owner's dashboard, and
/// because it is the one visit endpoint no seeker may reach.
/// </summary>
[ApiController]
[Route("api/v1/owner/visits")]
[Authorize(Policy = "VerifiedAccount", Roles = "Owner")]
public class OwnerVisitsController : ControllerBase
{
    private readonly IMediator _mediator;

    public OwnerVisitsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Visit requests on the signed-in owner's listings, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VisitRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<VisitRequestDto>>> GetForOwner(
        [FromQuery] GetOwnerVisitsQuery query)
    {
        query.OwnerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        return Ok(await _mediator.Send(query));
    }
}
