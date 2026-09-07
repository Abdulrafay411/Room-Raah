using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.AdminUsers.Commands;
using RoomRaah.Application.Features.AdminUsers.Queries;

namespace RoomRaah.API.Controllers;

/// <summary>
/// The accounts on the site. This area is in §5 and in the twenty-page structure and
/// belonged to no phase of the build plan — following the plan would have missed it.
///
/// The detail endpoint here is **the only place in the product that returns a phone
/// number or a CNIC**, and it is the single exception rule 33 names. The list carries
/// neither, because a page of fifty accounts is fifty identity numbers on one screen
/// for a question the list is not being asked.
/// </summary>
[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = "VerifiedAccount", Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminUsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Every account including the deactivated ones, newest first. Filter by role and
    /// active state, or search a name or an email address.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminUserSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<AdminUserSummaryDto>>> List(
        [FromQuery] GetAdminUsersQuery query)
    {
        return Ok(await _mediator.Send(query));
    }

    /// <summary>
    /// One account in full, including the phone number and — for an owner — the CNIC
    /// the identity check is about. Rule 116.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AdminUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDto>> GetOne(int id)
    {
        return Ok(await _mediator.Send(new GetAdminUserQuery { UserId = id }));
    }

    /// <summary>Switches an account off, with a reason. Rule 11: it can no longer sign in.</summary>
    [HttpPatch("{id:int}/deactivate")]
    [ProducesResponseType(typeof(AdminUserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<ActionResult<AdminUserSummaryDto>> Deactivate(
        int id, [FromBody] SetAccountActiveCommand command) => SetActiveAsync(id, command, false);

    /// <summary>
    /// Switches it back on. §5 listed only the deactivation, which made a misclick
    /// permanent and left an account no route back after whatever caused it was
    /// resolved.
    /// </summary>
    [HttpPatch("{id:int}/reactivate")]
    [ProducesResponseType(typeof(AdminUserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public Task<ActionResult<AdminUserSummaryDto>> Reactivate(
        int id, [FromBody] SetAccountActiveCommand command) => SetActiveAsync(id, command, true);

    /// <summary>
    /// The two endpoints differ only in the value they write, and the direction comes
    /// from the route rather than the body so a caller cannot ask one of them to do
    /// the other's work.
    /// </summary>
    private async Task<ActionResult<AdminUserSummaryDto>> SetActiveAsync(
        int id, SetAccountActiveCommand command, bool active)
    {
        command.UserId = id;
        command.AdminId = CurrentUserId();
        command.Active = active;

        return Ok(await _mediator.Send(command));
    }

    private int CurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
