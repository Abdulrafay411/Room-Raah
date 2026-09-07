using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Saved.Commands;
using RoomRaah.Application.Features.Saved.Queries;

namespace RoomRaah.API.Controllers;

/// <summary>
/// A shortlist. Private to one account and never cached — rule 99 — and open to any
/// verified account rather than to seekers alone, because §5 brackets the visit,
/// review and report rows by role and deliberately does not bracket this one.
/// </summary>
[ApiController]
[Route("api/v1/saved")]
[Authorize(Policy = "VerifiedAccount")]
public class SavedController : ControllerBase
{
    private readonly IMediator _mediator;

    public SavedController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>The signed-in account's shortlist, newest save first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SavedListingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<SavedListingDto>>> GetSaved(
        [FromQuery] GetSavedPropertiesQuery query)
    {
        // Overwritten after model binding, so a userId in the query string is
        // discarded rather than honoured.
        query.UserId = CurrentUserId();

        return Ok(await _mediator.Send(query));
    }

    /// <summary>Puts a published listing on the shortlist.</summary>
    [HttpPost("{propertyId:int}")]
    [ProducesResponseType(typeof(SavedListingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SavedListingDto>> Save(int propertyId)
    {
        var saved = await _mediator.Send(new SavePropertyCommand
        {
            UserId = CurrentUserId(),
            PropertyId = propertyId
        });

        return CreatedAtAction(nameof(GetSaved), null, saved);
    }

    /// <summary>Takes a listing off the shortlist. Works whatever became of the listing.</summary>
    [HttpDelete("{propertyId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unsave(int propertyId)
    {
        await _mediator.Send(new UnsavePropertyCommand
        {
            UserId = CurrentUserId(),
            PropertyId = propertyId
        });

        return NoContent();
    }

    private int CurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
