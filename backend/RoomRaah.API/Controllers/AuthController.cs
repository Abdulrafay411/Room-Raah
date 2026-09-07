using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Features.Auth.Commands;

namespace RoomRaah.API.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Creates a seeker or owner account and emails a verification code.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<UserSummaryDto>> Register([FromBody] RegisterCommand command)
    {
        var user = await _mediator.Send(command);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    /// <summary>Confirms an email address with the code that was sent to it, and signs the person in.</summary>
    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponseDto>> VerifyOtp([FromBody] VerifyOtpCommand command)
    {
        return Ok(await _mediator.Send(command));
    }

    /// <summary>Sends a fresh verification code, replacing any code already outstanding.</summary>
    [HttpPost("resend-otp")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpCommand command)
    {
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Exchanges email and password for an access token and a refresh token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginCommand command)
    {
        return Ok(await _mediator.Send(command));
    }

    /// <summary>Rotates a refresh token and issues a new access token.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponseDto>> Refresh([FromBody] RefreshCommand command)
    {
        return Ok(await _mediator.Send(command));
    }

    /// <summary>Revokes the given refresh token for the signed-in account.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand command)
    {
        command.AppUserId = CurrentUserId();
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Emails a single-use password reset link. Always succeeds, whether or not the account exists.</summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>Sets a new password using the token from the reset email, and ends every other session.</summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        await _mediator.Send(command);
        return NoContent();
    }

    private int CurrentUserId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
