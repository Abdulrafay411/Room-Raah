using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Reports.Commands;

namespace RoomRaah.API.Controllers;

/// <summary>
/// Flagging a listing that is wrong, misleading or already taken. There is no route
/// for this in the product: it is a dialog on the listing page, because moving
/// someone off the listing to fill in a form would lose them.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = "VerifiedAccount", Roles = "Seeker")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Files a report on a published listing. It is created Open.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MyReportDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MyReportDto>> Create([FromBody] CreateReportCommand command)
    {
        command.ReportedByUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var report = await _mediator.Send(command);

        return Created($"/api/v1/reports/{report.Id}", report);
    }
}
