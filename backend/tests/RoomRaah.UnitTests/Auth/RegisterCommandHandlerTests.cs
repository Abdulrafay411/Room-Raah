using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.Features.Auth.Commands;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Auth;

public class RegisterCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IOtpService _otp = Substitute.For<IOtpService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns("hashed");

        _handler = new RegisterCommandHandler(
            _users, _hasher, _otp, _unitOfWork, TestMapper.Create(),
            NullLogger<RegisterCommandHandler>.Instance);
    }

    private static RegisterCommand SeekerCommand() => new()
    {
        FullName = "Hamza Iqbal",
        Email = "Hamza@RoomRaah.local",
        PhoneNumber = "+923001112233",
        Password = "Password1",
        Role = "Seeker"
    };

    private static RegisterCommand OwnerCommand() => new()
    {
        FullName = "Kashif Mehmood",
        Email = "kashif@roomraah.local",
        PhoneNumber = "+923001112244",
        Password = "Password1",
        Role = "Owner",
        CnicNumber = "3520112345671",
        PayoutContact = "HBL 1234567890123"
    };

    [Fact]
    public async Task Creates_a_seeker_with_a_seeker_profile()
    {
        AppUser? created = null;
        await _users.CreateAsync(Arg.Do<AppUser>(u => created = u));

        var result = await _handler.Handle(SeekerCommand(), CancellationToken.None);

        created.ShouldNotBeNull();
        created!.Role.ShouldBe(UserRole.Seeker);
        created.SeekerProfile.ShouldNotBeNull();
        created.OwnerProfile.ShouldBeNull();
        result.Role.ShouldBe("Seeker");
    }

    [Fact]
    public async Task Creates_an_owner_with_an_owner_profile_carrying_the_cnic()
    {
        AppUser? created = null;
        await _users.CreateAsync(Arg.Do<AppUser>(u => created = u));

        await _handler.Handle(OwnerCommand(), CancellationToken.None);

        created!.OwnerProfile.ShouldNotBeNull();
        created.OwnerProfile!.CnicNumber.ShouldBe("3520112345671");
        created.OwnerProfile.IdentityStatus.ShouldBe(VerificationStatus.NotChecked);
        created.SeekerProfile.ShouldBeNull();
    }

    [Fact]
    public async Task Stores_the_email_lower_cased()
    {
        AppUser? created = null;
        await _users.CreateAsync(Arg.Do<AppUser>(u => created = u));

        await _handler.Handle(SeekerCommand(), CancellationToken.None);

        created!.Email.ShouldBe("hamza@roomraah.local");
    }

    [Fact]
    public async Task Never_stores_the_password_in_clear_text()
    {
        AppUser? created = null;
        await _users.CreateAsync(Arg.Do<AppUser>(u => created = u));

        await _handler.Handle(SeekerCommand(), CancellationToken.None);

        created!.PasswordHash.ShouldBe("hashed");
        created.PasswordHash.ShouldNotContain("Password1");
    }

    [Fact]
    public async Task Rule_4_a_new_account_is_not_verified()
    {
        AppUser? created = null;
        await _users.CreateAsync(Arg.Do<AppUser>(u => created = u));

        var result = await _handler.Handle(SeekerCommand(), CancellationToken.None);

        created!.IsEmailVerified.ShouldBeFalse();
        result.IsEmailVerified.ShouldBeFalse();
    }

    [Fact]
    public async Task Rule_1_a_duplicate_email_is_refused()
    {
        _users.EmailExistsAsync("hamza@roomraah.local").Returns(true);

        var act = () => _handler.Handle(SeekerCommand(), CancellationToken.None);

        var exception = await act.ShouldThrowAsync<BusinessRuleException>();
        exception.Message.ShouldContain("already exists");
    }

    [Fact]
    public async Task Issues_a_verification_code_after_the_account_is_saved()
    {
        await _handler.Handle(SeekerCommand(), CancellationToken.None);

        await _unitOfWork.Received(1).SaveChangesAsync();
        await _otp.Received(1).IssueAsync(Arg.Any<AppUser>(), OtpPurpose.VerifyEmail);
    }
}
