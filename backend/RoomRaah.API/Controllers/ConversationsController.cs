using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Messaging.Commands;
using RoomRaah.Application.Features.Messaging.Queries;

namespace RoomRaah.API.Controllers;

/// <summary>
/// Messaging, which is what §Contact Stays On the Site promises instead of a phone
/// number. Every action here creates or reads a record, so the whole controller needs
/// a verified account — rule 46 — and only opening a conversation is restricted
/// further, because only a seeker has anybody to address.
/// </summary>
[ApiController]
[Route("api/v1/conversations")]
[Authorize(Policy = "VerifiedAccount")]
public class ConversationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ConversationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Opens a conversation with the owner of a published listing, or hands back the
    /// one that is already open. Rule 43 makes the second case ordinary rather than an
    /// error, so it answers 200 where a new thread answers 201.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Seeker")]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDto>> Start(
        [FromBody] StartConversationCommand command)
    {
        command.SeekerId = CurrentUserId();

        var result = await _mediator.Send(command);

        return result.WasCreated
            ? CreatedAtAction(nameof(GetMine), null, result.Conversation)
            : Ok(result.Conversation);
    }

    /// <summary>The caller's own conversations, most recent message first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ConversationDto>>> GetMine(
        [FromQuery] GetConversationsQuery query)
    {
        query.UserId = CurrentUserId();

        return Ok(await _mediator.Send(query));
    }

    /// <summary>One thread, oldest message first.</summary>
    [HttpGet("{id:int}/messages")]
    [ProducesResponseType(typeof(PagedResult<MessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<MessageDto>>> GetMessages(
        int id, [FromQuery] GetMessagesQuery query)
    {
        query.ConversationId = id;
        query.UserId = CurrentUserId();

        return Ok(await _mediator.Send(query));
    }

    /// <summary>
    /// Posts a message. Pushed to the other participant if they are connected and
    /// emailed to them if they are not — rule 105.
    /// </summary>
    [HttpPost("{id:int}/messages")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageDto>> Send(
        int id, [FromBody] SendMessageCommand command)
    {
        command.ConversationId = id;
        command.SenderId = CurrentUserId();

        var message = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetMessages), new { id }, message);
    }

    /// <summary>
    /// Marks everything the caller did not send as read, and answers with how many.
    /// Rule 104: a thread with nothing unread is a success, not a refusal.
    /// </summary>
    [HttpPatch("{id:int}/read")]
    [ProducesResponseType(typeof(MarkReadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarkReadResultDto>> MarkRead(int id)
    {
        return Ok(await _mediator.Send(new MarkConversationReadCommand
        {
            ConversationId = id,
            ReaderId = CurrentUserId()
        }));
    }

    /// <summary>
    /// Removes the thread from the caller's inbox only. Rule 129: the other participant
    /// keeps their copy and no row is deleted; rule 131: a new message brings it back,
    /// holding only what came after.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new DeleteConversationCommand
        {
            ConversationId = id,
            UserId = CurrentUserId()
        });

        return NoContent();
    }

    private int CurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
