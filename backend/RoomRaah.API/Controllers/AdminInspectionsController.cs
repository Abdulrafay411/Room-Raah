using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Inspections.Commands;
using RoomRaah.Application.Features.Inspections.Queries;

namespace RoomRaah.API.Controllers;

/// <summary>
/// The physical inspection and the badge that follows it — the only paid thing in the
/// product, and the last part of it to be built because it sits on top of the admin
/// panel, the verification records and the owner dashboard.
///
/// Recording a visit and granting a badge need an admin who makes visits; removing one
/// does not, for the reason rule 111 gives. Nothing here returns a scheduled date,
/// because no such date exists anywhere in the product.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "VerifiedAccount", Roles = "Admin")]
public class AdminInspectionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminInspectionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Records a visit that has already happened. Rule 109.</summary>
    [HttpPost("inspections")]
    [ProducesResponseType(typeof(PropertyInspectionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PropertyInspectionDto>> Record(
        [FromBody] RecordInspectionCommand command)
    {
        command.AdminId = CurrentUserId();

        var inspection = await _mediator.Send(command);

        return CreatedAtAction(nameof(Due), null, inspection);
    }

    /// <summary>
    /// The re-inspection worklist: badged listings nobody has visited inside the
    /// interval, longest overdue first. A query rather than a sweep, because a WHERE
    /// clause is right without having had to run.
    /// </summary>
    [HttpGet("inspections/due")]
    [ProducesResponseType(typeof(PagedResult<DueForInspectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<DueForInspectionDto>>> Due(
        [FromQuery] GetDueForInspectionQuery query)
    {
        return Ok(await _mediator.Send(query));
    }

    /// <summary>
    /// Rule 136: listings whose owner asked for an inspection that has not happened yet,
    /// oldest request first. The due worklist only holds listings that already carry the
    /// badge, so a first inspection an owner asked for appears here instead.
    /// </summary>
    [HttpGet("inspections/requested")]
    [ProducesResponseType(typeof(PagedResult<RequestedInspectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<RequestedInspectionDto>>> Requested(
        [FromQuery] GetRequestedInspectionsQuery query)
    {
        return Ok(await _mediator.Send(query));
    }

    /// <summary>
    /// Turns the badge on. Refused unless the listing's most recent inspection passed,
    /// and refused for an admin who does not make visits.
    /// </summary>
    [HttpPost("properties/{id:int}/badge/grant")]
    [ProducesResponseType(typeof(AdminPropertyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminPropertyDto>> GrantBadge(
        int id, [FromBody] GrantBadgeCommand command)
    {
        command.PropertyId = id;
        command.AdminId = CurrentUserId();

        return Ok(await _mediator.Send(command));
    }

    /// <summary>
    /// Takes the badge off, with a reason. Any admin, because a claim being withdrawn
    /// should not wait for the one person who makes visits.
    /// </summary>
    [HttpPost("properties/{id:int}/badge/remove")]
    [ProducesResponseType(typeof(AdminPropertyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminPropertyDto>> RemoveBadge(
        int id, [FromBody] RemoveBadgeCommand command)
    {
        command.PropertyId = id;
        command.AdminId = CurrentUserId();

        return Ok(await _mediator.Send(command));
    }

    private int CurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
