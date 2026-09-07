using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Reviews.Queries;
using RoomRaah.Application.Features.Search.Queries;

namespace RoomRaah.API.Controllers;

/// <summary>
/// The public front door: search, the landing page, one listing, its gallery and a
/// comparison. Anonymous by design — requiring a token before a stranger can see a
/// single room would defeat the product.
///
/// Everything here reads published listings only, and nothing here names a way to
/// reach an owner. Both of those are enforced below the controller, in the repository
/// and in the mapping profile, so a route added later inherits them.
/// </summary>
[ApiController]
[Route("api/v1/properties")]
[AllowAnonymous]
public class PropertiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PropertiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Searches published listings, filtered and sorted as the query asks.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ListingCardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResult<ListingCardDto>>> Search(
        [FromQuery] SearchPropertiesQuery query)
    {
        return Ok(await _mediator.Send(query));
    }

    /// <summary>The landing page: up to eight listings, inspected ones first.</summary>
    [HttpGet("featured")]
    [ProducesResponseType(typeof(List<ListingCardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ListingCardDto>>> Featured()
    {
        return Ok(await _mediator.Send(new GetFeaturedPropertiesQuery()));
    }

    /// <summary>Compares up to three listings side by side.</summary>
    [HttpGet("compare")]
    [ProducesResponseType(typeof(List<ListingComparisonDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<List<ListingComparisonDto>>> Compare(
        [FromQuery] ComparePropertiesQuery query)
    {
        return Ok(await _mediator.Send(query));
    }

    /// <summary>One published listing in full, with its gallery, facilities and checks.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ListingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ListingDetailDto>> GetOne(int id)
    {
        return Ok(await _mediator.Send(new GetPropertyQuery { PropertyId = id }));
    }

    /// <summary>
    /// The approved reviews of one published listing, newest first, with the average
    /// rating and the count. Rule 41: a review that no admin has approved is not here,
    /// and rule 95 keeps every one of them to a display name.
    /// </summary>
    [HttpGet("{id:int}/reviews")]
    [ProducesResponseType(typeof(PropertyReviewsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PropertyReviewsDto>> GetReviews(
        int id, [FromQuery] GetPropertyReviewsQuery query)
    {
        // Overwritten after model binding: the id in the route is the listing, and a
        // propertyId in the query string is not a second opinion about which one.
        query.PropertyId = id;

        return Ok(await _mediator.Send(query));
    }

    /// <summary>The public gallery of one published listing, in order.</summary>
    [HttpGet("{id:int}/photos")]
    [ProducesResponseType(typeof(List<ListingPhotoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ListingPhotoDto>>> GetPhotos(int id)
    {
        return Ok(await _mediator.Send(new GetPropertyPhotosQuery { PropertyId = id }));
    }
}
