using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Properties.Commands;
using RoomRaah.Application.Features.Inspections.Commands;
using RoomRaah.Application.Features.Properties.Queries;

namespace RoomRaah.API.Controllers;

/// <summary>
/// An owner's own listings. Nothing here is public and nothing here is cached:
/// every listing in this area is either unpublished or private to one account.
/// </summary>
[ApiController]
[Route("api/v1/owner/properties")]
[Authorize(Policy = "VerifiedAccount", Roles = "Owner")]
public class OwnerPropertiesController : ControllerBase
{
    private readonly IMediator _mediator;

    public OwnerPropertiesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Creates a listing as a draft. It is visible to nobody until it is submitted and approved.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OwnerPropertyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OwnerPropertyDto>> Create([FromBody] CreatePropertyCommand command)
    {
        command.OwnerId = CurrentUserId();

        var property = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetOne), new { id = property.Id }, property);
    }

    /// <summary>Lists the signed-in owner's listings, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OwnerPropertySummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OwnerPropertySummaryDto>>> GetMine(
        [FromQuery] GetMyPropertiesQuery query)
    {
        // Overwritten after model binding, so an ownerId in the query string is
        // discarded rather than honoured.
        query.OwnerId = CurrentUserId();

        return Ok(await _mediator.Send(query));
    }

    /// <summary>Returns one of the signed-in owner's listings, with its photos and facilities.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OwnerPropertyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OwnerPropertyDto>> GetOne(int id)
    {
        return Ok(await _mediator.Send(new GetMyPropertyQuery
        {
            PropertyId = id,
            OwnerId = CurrentUserId()
        }));
    }

    /// <summary>
    /// Asks RoomRaah to inspect the listing in person — once, and only once. Rule 108:
    /// after the first visit the team goes on its own schedule, and no response here
    /// or anywhere else ever names a date that has not happened yet.
    /// </summary>
    [HttpPost("{id:int}/inspection-request")]
    [ProducesResponseType(typeof(OwnerInspectionStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OwnerInspectionStateDto>> RequestInspection(int id)
    {
        return Ok(await _mediator.Send(new RequestInspectionCommand
        {
            PropertyId = id,
            OwnerId = CurrentUserId()
        }));
    }

    /// <summary>Sends a draft for review. This is the only path to PendingReview.</summary>
    [HttpPost("{id:int}/submit")]
    [ProducesResponseType(typeof(OwnerPropertyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OwnerPropertyDto>> Submit(int id)
    {
        return Ok(await _mediator.Send(new SubmitPropertyCommand
        {
            PropertyId = id,
            OwnerId = CurrentUserId()
        }));
    }

    /// <summary>
    /// Edits a listing. On a published one this creates a revision and leaves the
    /// live listing exactly as it was; on a draft it writes the change straight in.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(OwnerListingUpdateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OwnerListingUpdateDto>> Update(
        int id, [FromBody] UpdatePropertyCommand command)
    {
        command.PropertyId = id;
        command.OwnerId = CurrentUserId();

        return Ok(await _mediator.Send(command));
    }

    /// <summary>
    /// Confirms how many beds are free. Sending the count the listing already shows
    /// is a confirmation rather than a no-op: it is what keeps the listing off the
    /// stale list.
    /// </summary>
    [HttpPatch("{id:int}/availability")]
    [ProducesResponseType(typeof(OwnerListingUpdateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OwnerListingUpdateDto>> ConfirmAvailability(
        int id, [FromBody] ConfirmAvailabilityCommand command)
    {
        command.PropertyId = id;
        command.OwnerId = CurrentUserId();

        return Ok(await _mediator.Send(command));
    }

    /// <summary>Adds one photo to a listing and generates its thumbnail.</summary>
    [HttpPost("{id:int}/photos")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(PropertyPhotoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PropertyPhotoDto>> UploadPhoto(int id, IFormFile? file)
    {
        await using var content = file?.OpenReadStream() ?? Stream.Null;

        var photo = await _mediator.Send(new UploadPropertyPhotoCommand
        {
            PropertyId = id,
            OwnerId = CurrentUserId(),
            FileName = file?.FileName ?? string.Empty,
            ContentType = file?.ContentType ?? string.Empty,
            Length = file?.Length ?? 0,
            Content = content
        });

        return StatusCode(StatusCodes.Status201Created, photo);
    }

    /// <summary>Removes one photo from a listing, along with its stored files.</summary>
    [HttpDelete("{id:int}/photos/{photoId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeletePhoto(int id, int photoId)
    {
        await _mediator.Send(new DeletePropertyPhotoCommand
        {
            PropertyId = id,
            PhotoId = photoId,
            OwnerId = CurrentUserId()
        });

        return NoContent();
    }

    /// <summary>Sets the gallery order. The body must name every photo the listing will hold, exactly once.</summary>
    [HttpPatch("{id:int}/photos/order")]
    [ProducesResponseType(typeof(OwnerListingUpdateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OwnerListingUpdateDto>> ReorderPhotos(
        int id, [FromBody] ReorderPropertyPhotosCommand command)
    {
        command.PropertyId = id;
        command.OwnerId = CurrentUserId();

        return Ok(await _mediator.Send(command));
    }

    /// <summary>Chooses the photo shown on the listing card.</summary>
    [HttpPatch("{id:int}/photos/{photoId:int}/primary")]
    [ProducesResponseType(typeof(OwnerListingUpdateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OwnerListingUpdateDto>> SetPrimaryPhoto(int id, int photoId)
    {
        return Ok(await _mediator.Send(new SetPrimaryPropertyPhotoCommand
        {
            PropertyId = id,
            PhotoId = photoId,
            OwnerId = CurrentUserId()
        }));
    }

    private int CurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
