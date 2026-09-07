using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Locations.Queries;

namespace RoomRaah.API.Controllers;

/// <summary>
/// Location reference data. Anonymous, because the search box on the landing page
/// is the first thing a visitor uses and it cannot ask for a token first.
/// </summary>
[ApiController]
[Route("api/v1/locations")]
[AllowAnonymous]
public class LocationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public LocationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lists every city the product has listings in, alphabetically.</summary>
    [HttpGet("cities")]
    [ProducesResponseType(typeof(List<CityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CityDto>>> GetCities()
    {
        return Ok(await _mediator.Send(new GetCitiesQuery()));
    }

    /// <summary>Lists the areas within one city, alphabetically.</summary>
    [HttpGet("cities/{cityId:int}/areas")]
    [ProducesResponseType(typeof(List<AreaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<AreaDto>>> GetAreas(int cityId)
    {
        return Ok(await _mediator.Send(new GetAreasQuery { CityId = cityId }));
    }

    /// <summary>Suggests cities, areas and landmarks matching what has been typed so far.</summary>
    [HttpGet("suggest")]
    [ProducesResponseType(typeof(List<LocationSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<LocationSuggestionDto>>> Suggest(
        [FromQuery] SuggestLocationsQuery query)
    {
        return Ok(await _mediator.Send(query));
    }
}
