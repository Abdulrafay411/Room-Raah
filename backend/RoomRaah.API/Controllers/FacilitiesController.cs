using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Facilities.Queries;

namespace RoomRaah.API.Controllers;

/// <summary>
/// The fixed facility reference list every filter panel is built from. Anonymous,
/// for the same reason the location lists are.
/// </summary>
[ApiController]
[Route("api/v1/facilities")]
[AllowAnonymous]
public class FacilitiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public FacilitiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lists every facility a listing can offer, in display order.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<FacilityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FacilityDto>>> GetFacilities()
    {
        return Ok(await _mediator.Send(new GetFacilitiesQuery()));
    }
}
