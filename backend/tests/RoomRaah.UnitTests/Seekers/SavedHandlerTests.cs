using NSubstitute;
using RoomRaah.Application.Features.Saved.Commands;
using RoomRaah.Application.Features.Saved.Queries;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;
using RoomRaah.UnitTests.Common;
using Shouldly;

namespace RoomRaah.UnitTests.Seekers;

public class SavedHandlerTests
{
    private const int UserId = 7;

    private readonly IPropertyRepository _properties = Substitute.For<IPropertyRepository>();
    private readonly ISavedPropertyRepository _saved = Substitute.For<ISavedPropertyRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private SavePropertyCommandHandler Save() =>
        new(_properties, _saved, _unitOfWork, TestMapper.Create());

    private UnsavePropertyCommandHandler Unsave() => new(_saved, _unitOfWork);

    private GetSavedPropertiesQueryHandler List() => new(_saved, TestMapper.Create());

    [Fact]
    public async Task A_published_listing_goes_onto_the_shortlist_as_a_card()
    {
        _properties.GetPublishedAsync(1).Returns(
            TestData.ReviewableProperty(1, status: PropertyStatus.Published));
        _saved.GetAsync(UserId, 1).Returns((SavedProperty?)null);

        var card = await Save().Handle(
            new SavePropertyCommand { UserId = UserId, PropertyId = 1 }, default);

        card.Id.ShouldBe(1);
        card.Title.ShouldNotBeNullOrWhiteSpace();
        card.CityName.ShouldNotBeNullOrWhiteSpace();
        card.SavedAt.ShouldNotBe(default);

        await _saved.Received(1).CreateAsync(Arg.Is<SavedProperty>(
            s => s.UserId == UserId && s.PropertyId == 1));
        await _unitOfWork.Received(1).SaveChangesAsync();
    }

    /// <summary>
    /// Rule 86. The same 404 a stranger gets: a different answer here would tell a
    /// signed-in visitor that an unpublished listing exists.
    /// </summary>
    [Fact]
    public async Task Rule_86_a_listing_that_is_not_published_cannot_be_saved()
    {
        _properties.GetPublishedAsync(1).Returns((Property?)null);

        await Should.ThrowAsync<NotFoundException>(Save().Handle(
            new SavePropertyCommand { UserId = UserId, PropertyId = 1 }, default));

        await _saved.DidNotReceive().CreateAsync(Arg.Any<SavedProperty>());
    }

    [Fact]
    public async Task Rule_34_the_same_listing_cannot_be_saved_twice()
    {
        _properties.GetPublishedAsync(1).Returns(
            TestData.ReviewableProperty(1, status: PropertyStatus.Published));
        _saved.GetAsync(UserId, 1).Returns(TestData.Saved(userId: UserId));

        await Should.ThrowAsync<BusinessRuleException>(Save().Handle(
            new SavePropertyCommand { UserId = UserId, PropertyId = 1 }, default));

        await _saved.DidNotReceive().CreateAsync(Arg.Any<SavedProperty>());
    }

    [Fact]
    public async Task Removing_a_listing_that_is_not_on_the_shortlist_is_not_found()
    {
        _saved.GetAsync(UserId, 1).Returns((SavedProperty?)null);

        await Should.ThrowAsync<NotFoundException>(Unsave().Handle(
            new UnsavePropertyCommand { UserId = UserId, PropertyId = 1 }, default));
    }

    /// <summary>
    /// Rule 87. The unsave path never asks whether the listing is still published,
    /// because a shortlist holding a row nobody can remove is worse than one holding
    /// a listing that has gone.
    /// </summary>
    [Fact]
    public async Task Rule_87_a_listing_that_has_left_the_public_site_can_still_be_removed()
    {
        _saved.GetAsync(UserId, 1).Returns(TestData.Saved(userId: UserId));

        await Unsave().Handle(new UnsavePropertyCommand { UserId = UserId, PropertyId = 1 }, default);

        _saved.Received(1).Remove(Arg.Any<SavedProperty>());
        await _unitOfWork.Received(1).SaveChangesAsync();
        await _properties.DidNotReceive().GetPublishedAsync(Arg.Any<int>());
    }

    [Fact]
    public async Task The_shortlist_carries_the_moment_each_listing_was_saved()
    {
        var first = DateTime.UtcNow.AddDays(-1);
        var second = DateTime.UtcNow.AddDays(-3);

        _saved.GetPageAsync(UserId, 1, 12).Returns([
            TestData.Saved(1, UserId, 1, TestData.ReviewableProperty(1, status: PropertyStatus.Published), first),
            TestData.Saved(2, UserId, 2, TestData.ReviewableProperty(2, status: PropertyStatus.Published), second)
        ]);
        _saved.CountAsync(UserId).Returns(2);

        var page = await List().Handle(
            new GetSavedPropertiesQuery { UserId = UserId, Page = 1, PageSize = 12 }, default);

        page.TotalCount.ShouldBe(2);
        page.Items.Select(i => i.SavedAt).ShouldBe([first, second]);
        page.Items.Select(i => i.Id).ShouldBe([1, 2]);
    }
}
