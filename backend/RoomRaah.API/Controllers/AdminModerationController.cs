using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Reports.Commands;
using RoomRaah.Application.Features.Reports.Queries;
using RoomRaah.Application.Features.Reviews.Commands;
using RoomRaah.Application.Features.Reviews.Queries;

namespace RoomRaah.API.Controllers;

/// <summary>
/// Reviews and reports on the admin's side. The review queue is what makes rule 41 a
/// rule rather than a wall: every review is created Pending, and without somewhere to
/// approve one the public review list would stay empty for the life of the product.
/// Nothing here is cached, and nothing here returns anybody's email address.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "VerifiedAccount", Roles = "Admin")]
public class AdminModerationController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminModerationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Reviews in one moderation state, the one waiting longest first.</summary>
    [HttpGet("reviews")]
    [ProducesResponseType(typeof(PagedResult<AdminReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<AdminReviewDto>>> ReviewQueue(
        [FromQuery] GetReviewQueueQuery query)
    {
        return Ok(await _mediator.Send(query));
    }

    /// <summary>Approves or hides one review.</summary>
    [HttpPatch("reviews/{id:int}")]
    [ProducesResponseType(typeof(AdminReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminReviewDto>> Moderate(
        int id, [FromBody] ModerateReviewCommand command)
    {
        command.ReviewId = id;
        command.AdminId = CurrentAdminId();

        return Ok(await _mediator.Send(command));
    }

    /// <summary>Reports in one status, the one waiting longest first.</summary>
    [HttpGet("reports")]
    [ProducesResponseType(typeof(PagedResult<ReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReportDto>>> ReportQueue(
        [FromQuery] GetReportQueueQuery query)
    {
        return Ok(await _mediator.Send(query));
    }

    /// <summary>
    /// Moves a report to UnderReview, Upheld or Dismissed. Rule 98: a verdict is
    /// recorded and the listing is left where it is.
    /// </summary>
    [HttpPatch("reports/{id:int}")]
    [ProducesResponseType(typeof(ReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReportDto>> Resolve(int id, [FromBody] ResolveReportCommand command)
    {
        command.ReportId = id;
        command.AdminId = CurrentAdminId();

        return Ok(await _mediator.Send(command));
    }

    private int CurrentAdminId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
