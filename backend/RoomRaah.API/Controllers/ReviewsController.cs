using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Reviews.Commands;

namespace RoomRaah.API.Controllers;

/// <summary>
/// Writing a review. Reading them is anonymous and lives on the public listing
/// route, because a review is part of what a stranger reads before signing up.
/// </summary>
[ApiController]
[Route("api/v1/reviews")]
[Authorize(Policy = "VerifiedAccount", Roles = "Seeker")]
public class ReviewsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReviewsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Writes a review of a listing the author has completed a visit to. It is
    /// created Pending and is invisible until an admin approves it: rule 41.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MyReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MyReviewDto>> Create([FromBody] CreateReviewCommand command)
    {
        command.AuthorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var review = await _mediator.Send(command);

        return Created($"/api/v1/properties/{review.PropertyId}/reviews", review);
    }
}
