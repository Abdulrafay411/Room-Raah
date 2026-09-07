using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Visits.Commands;
using RoomRaah.Application.Features.Visits.Queries;

namespace RoomRaah.API.Controllers;

/// <summary>
/// Visit requests, which are the only way a visit is arranged in this product: there
/// are no call buttons and no WhatsApp links anywhere in it. The roles differ per
/// action, so they are declared per action — completing is the one both sides share,
/// for the reason rule 90 gives.
/// </summary>
[ApiController]
[Route("api/v1/visits")]
[Authorize(Policy = "VerifiedAccount")]
public class VisitsController : ControllerBase
{
    private readonly IMediator _mediator;

    public VisitsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Asks the owner of a published listing for a physical or video visit.</summary>
    [HttpPost]
    [Authorize(Roles = "Seeker")]
    [ProducesResponseType(typeof(VisitRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VisitRequestDto>> Create([FromBody] CreateVisitRequestCommand command)
    {
        command.SeekerId = CurrentUserId();

        var visit = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetMine), null, visit);
    }

    /// <summary>The signed-in seeker's own visit requests, newest first.</summary>
    [HttpGet("my")]
    [Authorize(Roles = "Seeker")]
    [ProducesResponseType(typeof(PagedResult<VisitRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<VisitRequestDto>>> GetMine(
        [FromQuery] GetMyVisitsQuery query)
    {
        query.SeekerId = CurrentUserId();

        return Ok(await _mediator.Send(query));
    }

    /// <summary>Accepts or declines a request on one of the owner's own listings.</summary>
    [HttpPatch("{id:int}/respond")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(VisitRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VisitRequestDto>> Respond(
        int id, [FromBody] RespondToVisitCommand command)
    {
        command.VisitId = id;
        command.OwnerId = CurrentUserId();

        return Ok(await _mediator.Send(command));
    }

    /// <summary>
    /// Says the visit happened. Rule 90: either the seeker who asked or the owner of
    /// the listing, because a review depends on this and an owner alone would hold a
    /// veto over every review of their own property.
    /// </summary>
    [HttpPatch("{id:int}/complete")]
    [Authorize(Roles = "Seeker,Owner")]
    [ProducesResponseType(typeof(VisitRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VisitRequestDto>> Complete(int id)
    {
        return Ok(await _mediator.Send(new CompleteVisitCommand
        {
            VisitId = id,
            UserId = CurrentUserId()
        }));
    }

    /// <summary>Withdraws a request. Rule 91: the seeker who made it, and nobody else.</summary>
    [HttpPatch("{id:int}/cancel")]
    [Authorize(Roles = "Seeker")]
    [ProducesResponseType(typeof(VisitRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VisitRequestDto>> Cancel(int id)
    {
        return Ok(await _mediator.Send(new CancelVisitCommand
        {
            VisitId = id,
            SeekerId = CurrentUserId()
        }));
    }

    private int CurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
