using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.AdminListings.Commands;
using RoomRaah.Application.Features.AdminListings.Queries;

namespace RoomRaah.API.Controllers;

/// <summary>
/// The review side of a listing. Nothing here is cached: a queue is unpublished work
/// read by a handful of people and changed by every decision they make. Nothing here
/// returns an owner's phone number or email address either — rule 33 has no exception
/// for an admin panel.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "VerifiedAccount", Roles = "Admin")]
public class AdminListingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminListingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lists listings in one status, the one waiting longest first.</summary>
    [HttpGet("properties")]
    [ProducesResponseType(typeof(PagedResult<AdminPropertySummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<AdminPropertySummaryDto>>> Queue(
        [FromQuery] GetListingQueueQuery query)
    {
        return Ok(await _mediator.Send(query));
    }

    /// <summary>One listing with its photos, facilities, verification checks and owner.</summary>
    [HttpGet("properties/{id:int}")]
    [ProducesResponseType(typeof(AdminPropertyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPropertyDto>> GetOne(int id)
    {
        return Ok(await _mediator.Send(new GetListingForReviewQuery { PropertyId = id }));
    }

    /// <summary>
    /// Takes a published listing off the site, with a note saying why. Rule 122: §5 had
    /// ten admin listing endpoints and none of them set <c>Unpublished</c>, so the
    /// status existed in the enumeration and could never be reached.
    /// </summary>
    [HttpPost("properties/{id:int}/unpublish")]
    [ProducesResponseType(typeof(AdminPropertyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminPropertyDto>> Unpublish(
        int id, [FromBody] UnpublishListingCommand command)
    {
        command.PropertyId = id;
        command.AdminId = CurrentUserId();

        return Ok(await _mediator.Send(command));
    }

    /// <summary>Publishes a listing. Refused until all four verification checks have passed.</summary>
    [HttpPost("properties/{id:int}/approve")]
    [ProducesResponseType(typeof(AdminPropertyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminPropertyDto>> Approve(
        int id, [FromBody] ApproveListingCommand? command)
    {
        command ??= new ApproveListingCommand();
        command.PropertyId = id;
        command.AdminId = CurrentUserId();

        return Ok(await _mediator.Send(command));
    }

    /// <summary>Rejects a listing. The note is required and is sent to the owner.</summary>
    [HttpPost("properties/{id:int}/reject")]
    [ProducesResponseType(typeof(AdminPropertyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminPropertyDto>> Reject(
        int id, [FromBody] RejectListingCommand? command)
    {
        command ??= new RejectListingCommand();
        command.PropertyId = id;
        command.AdminId = CurrentUserId();

        return Ok(await _mediator.Send(command));
    }

    /// <summary>Sends a listing back to its owner. The note is the message they read.</summary>
    [HttpPost("properties/{id:int}/request-changes")]
    [ProducesResponseType(typeof(AdminPropertyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminPropertyDto>> RequestChanges(
        int id, [FromBody] RequestListingChangesCommand? command)
    {
        command ??= new RequestListingChangesCommand();
        command.PropertyId = id;
        command.AdminId = CurrentUserId();

        return Ok(await _mediator.Send(command));
    }

    /// <summary>Records one verification check, replacing any earlier verdict of the same type.</summary>
    [HttpPost("properties/{id:int}/verify")]
    [ProducesResponseType(typeof(List<VerificationCheckDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<VerificationCheckDto>>> Verify(
        int id, [FromBody] RecordVerificationCheckCommand command)
    {
        command.PropertyId = id;
        command.AdminId = CurrentUserId();

        return Ok(await _mediator.Send(command));
    }

    /// <summary>Lists pending revisions of one kind, the one waiting longest first.</summary>
    [HttpGet("revisions")]
    [ProducesResponseType(typeof(PagedResult<RevisionQueueRowDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<RevisionQueueRowDto>>> Revisions(
        [FromQuery] GetRevisionQueueQuery query)
    {
        return Ok(await _mediator.Send(query));
    }

    /// <summary>One revision, with the live values beside it and the photos it moves.</summary>
    [HttpGet("revisions/{id:int}")]
    [ProducesResponseType(typeof(RevisionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RevisionDetailDto>> GetRevision(int id)
    {
        return Ok(await _mediator.Send(new GetRevisionQuery { RevisionId = id }));
    }

    /// <summary>Applies a revision to its listing.</summary>
    [HttpPost("revisions/{id:int}/approve")]
    [ProducesResponseType(typeof(RevisionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RevisionDetailDto>> ApproveRevision(
        int id, [FromBody] ApproveRevisionCommand? command)
    {
        command ??= new ApproveRevisionCommand();
        command.RevisionId = id;
        command.AdminId = CurrentUserId();

        return Ok(await _mediator.Send(command));
    }

    /// <summary>Discards a revision. The live listing is left exactly as it was.</summary>
    [HttpPost("revisions/{id:int}/reject")]
    [ProducesResponseType(typeof(RevisionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RevisionDetailDto>> RejectRevision(
        int id, [FromBody] RejectRevisionCommand? command)
    {
        command ??= new RejectRevisionCommand();
        command.RevisionId = id;
        command.AdminId = CurrentUserId();

        return Ok(await _mediator.Send(command));
    }

    /// <summary>
    /// The route and the token decide which listing and which admin, never the body.
    /// Both are assigned after model binding, so a property id or an admin id sent in
    /// the body is overwritten rather than honoured.
    /// </summary>
    private int CurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
