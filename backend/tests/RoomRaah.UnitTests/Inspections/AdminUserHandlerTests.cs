using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoomRaah.Application.Caching;
using RoomRaah.Application.Features.AdminUsers.Commands;
using RoomRaah.Application.Features.AdminUsers.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Inspections;

public class AdminUserHandlerTests
{
    private const int AdminId = 1;
    private const int SeekerId = 2;
    private const int OwnerId = 3;

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMapper _mapper = TestMapper.Create();

    private GetAdminUsersQueryHandler List() => new(_users, _mapper);

    private GetAdminUserQueryHandler One() => new(_users, _mapper);

    private SetAccountActiveCommandHandler SetActive() =>
        new(_users, _cache, _unitOfWork, _mapper,
            NullLogger<SetAccountActiveCommandHandler>.Instance);

    private static AppUser Owner(int id = OwnerId, bool isActive = true)
    {
        var user = TestData.User(id, "owner@roomraah.local", UserRole.Owner, isActive);

        user.OwnerProfile = new OwnerProfile
        {
            AppUserId = id,
            CnicNumber = "3520112345671",
            IdentityStatus = VerificationStatus.Checked,
            IdentityCheckedAt = DateTime.UtcNow.AddDays(-5)
        };

        return user;
    }

    // -------------------------------------------------------- list (rule 117)

    [Fact]
    public async Task The_list_carries_no_identity_number()
    {
        _users.GetPageAsync(null, null, null, 1, 12).Returns([Owner()]);
        _users.CountPageAsync(null, null, null).Returns(1);

        var page = await List().Handle(new GetAdminUsersQuery(), default);

        // The type itself has no such property; this asserts the mapping is not
        // producing something wider than the type by another route.
        page.Items.Single().GetType().GetProperties()
            .Select(p => p.Name)
            .ShouldNotContain("CnicNumber");
    }

    [Fact]
    public async Task A_role_filter_is_passed_through_as_a_role()
    {
        _users.GetPageAsync(Arg.Any<UserRole?>(), Arg.Any<bool?>(), Arg.Any<string?>(), 1, 12)
            .Returns([]);

        await List().Handle(new GetAdminUsersQuery { Role = "owner" }, default);

        await _users.Received(1).GetPageAsync(UserRole.Owner, null, null, 1, 12);
    }

    /// <summary>
    /// A search of nothing but spaces is no search at all, and passing it through would
    /// turn a LIKE into a match on every row rather than on none.
    /// </summary>
    [Fact]
    public async Task A_blank_search_is_no_search()
    {
        _users.GetPageAsync(Arg.Any<UserRole?>(), Arg.Any<bool?>(), Arg.Any<string?>(), 1, 12)
            .Returns([]);

        await List().Handle(new GetAdminUsersQuery { Q = "   " }, default);

        await _users.Received(1).GetPageAsync(null, null, null, 1, 12);
    }

    // ------------------------------------------------------ detail (rule 116)

    [Fact]
    public async Task The_detail_carries_the_identity_the_check_is_about()
    {
        _users.GetByIdAsync(OwnerId).Returns(Owner());

        var dto = await One().Handle(new GetAdminUserQuery { UserId = OwnerId }, default);

        dto.CnicNumber.ShouldBe("3520112345671");
        dto.PhoneNumber.ShouldNotBeNullOrWhiteSpace();
        dto.IdentityStatus.ShouldBe(nameof(VerificationStatus.Checked));
    }

    /// <summary>Only an owner has a CNIC, so only an owner's detail carries one.</summary>
    [Fact]
    public async Task A_seeker_has_no_identity_number_to_show()
    {
        _users.GetByIdAsync(SeekerId).Returns(TestData.User(SeekerId));

        var dto = await One().Handle(new GetAdminUserQuery { UserId = SeekerId }, default);

        dto.CnicNumber.ShouldBeNull();
        dto.IdentityStatus.ShouldBeNull();
        dto.CanInspect.ShouldBeNull();
    }

    [Fact]
    public async Task An_account_that_does_not_exist_is_a_404()
    {
        _users.GetByIdAsync(404).Returns((AppUser?)null);

        await Should.ThrowAsync<NotFoundException>(() => One().Handle(
            new GetAdminUserQuery { UserId = 404 }, default));
    }

    // -------------------------------------------------- set active (118-121)

    [Fact]
    public async Task An_account_is_switched_off_with_a_reason()
    {
        var seeker = TestData.User(SeekerId);
        _users.GetByIdAsync(SeekerId).Returns(seeker);

        var dto = await SetActive().Handle(new SetAccountActiveCommand
        {
            UserId = SeekerId,
            AdminId = AdminId,
            Active = false,
            Reason = "Repeated abusive messages."
        }, default);

        dto.IsActive.ShouldBeFalse();
        seeker.IsActive.ShouldBeFalse();

        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task An_account_is_switched_back_on()
    {
        var seeker = TestData.User(SeekerId, isActive: false);
        _users.GetByIdAsync(SeekerId).Returns(seeker);

        var dto = await SetActive().Handle(new SetAccountActiveCommand
        {
            UserId = SeekerId,
            AdminId = AdminId,
            Active = true,
            Reason = "The report against this account was dismissed."
        }, default);

        dto.IsActive.ShouldBeTrue();
    }

    /// <summary>
    /// Rule 120. Deactivating an owner drops the cache, because a listing already
    /// sitting in a cached search page does not leave it because a query filter changed.
    /// </summary>
    [Fact]
    public async Task Deactivating_an_owner_drops_the_listing_cache()
    {
        _users.GetByIdAsync(OwnerId).Returns(Owner());

        await SetActive().Handle(new SetAccountActiveCommand
        {
            UserId = OwnerId,
            AdminId = AdminId,
            Active = false,
            Reason = "Listings do not match the properties."
        }, default);

        await _cache.Received(1).RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);
    }

    /// <summary>A seeker owns no listings, so nothing about the public site changed.</summary>
    [Fact]
    public async Task Deactivating_a_seeker_leaves_the_listing_cache_alone()
    {
        _users.GetByIdAsync(SeekerId).Returns(TestData.User(SeekerId));

        await SetActive().Handle(new SetAccountActiveCommand
        {
            UserId = SeekerId,
            AdminId = AdminId,
            Active = false,
            Reason = "Repeated abusive messages."
        }, default);

        await _cache.DidNotReceive().RemoveByPrefixAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task An_admin_cannot_switch_off_their_own_account()
    {
        _users.GetByIdAsync(AdminId).Returns(TestData.User(AdminId, "admin@roomraah.local", UserRole.Admin));

        await Should.ThrowAsync<BusinessRuleException>(() => SetActive().Handle(
            new SetAccountActiveCommand
            {
                UserId = AdminId,
                AdminId = AdminId,
                Active = false,
                Reason = "Because."
            }, default));
    }

    [Fact]
    public async Task An_admin_cannot_switch_off_another_admin()
    {
        _users.GetByIdAsync(9).Returns(TestData.User(9, "admin2@roomraah.local", UserRole.Admin));

        await Should.ThrowAsync<UnauthorizedAccessException>(() => SetActive().Handle(
            new SetAccountActiveCommand
            {
                UserId = 9,
                AdminId = AdminId,
                Active = false,
                Reason = "Because."
            }, default));
    }

    [Fact]
    public async Task Switching_an_account_to_the_state_it_is_in_is_refused()
    {
        _users.GetByIdAsync(SeekerId).Returns(TestData.User(SeekerId, isActive: false));

        await Should.ThrowAsync<BusinessRuleException>(() => SetActive().Handle(
            new SetAccountActiveCommand
            {
                UserId = SeekerId,
                AdminId = AdminId,
                Active = false,
                Reason = "Because."
            }, default));
    }

    /// <summary>Rule 119, and it is asked before the state rules for the usual reason.</summary>
    [Fact]
    public async Task A_change_with_no_reason_is_a_bad_request()
    {
        _users.GetByIdAsync(SeekerId).Returns(TestData.User(SeekerId));

        await Should.ThrowAsync<FluentValidation.ValidationException>(() => SetActive().Handle(
            new SetAccountActiveCommand
            {
                UserId = SeekerId,
                AdminId = AdminId,
                Active = false,
                Reason = null
            }, default));

        await _unitOfWork.DidNotReceive().SaveChangesAsync();
    }
}
