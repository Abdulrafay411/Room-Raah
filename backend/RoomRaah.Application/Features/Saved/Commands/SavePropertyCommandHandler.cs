using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Application.Seekers;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Saved.Commands;

public class SavePropertyCommandHandler : IRequestHandler<SavePropertyCommand, SavedListingDto>
{
    private readonly IPropertyRepository _properties;
    private readonly ISavedPropertyRepository _saved;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SavePropertyCommandHandler(
        IPropertyRepository properties,
        ISavedPropertyRepository saved,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _properties = properties;
        _saved = saved;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<SavedListingDto> Handle(SavePropertyCommand request, CancellationToken cancellationToken)
    {
        // Rule 86. A shortlist names a listing the way a stranger does, so a listing
        // that is not published cannot be saved at all — and the refusal is the same
        // 404 the detail route gives, because a different answer here would tell a
        // signed-in visitor that an unpublished listing exists.
        var property = await _properties.GetPublishedAsync(request.PropertyId)
            ?? throw new NotFoundException(SearchMessages.ListingNotFound(request.PropertyId));

        // Rule 34.
        if (await _saved.GetAsync(request.UserId, request.PropertyId) is not null)
        {
            throw new BusinessRuleException(SeekerMessages.AlreadySaved);
        }

        var saved = new SavedProperty
        {
            UserId = request.UserId,
            PropertyId = property.Id
        };

        await _saved.CreateAsync(saved);
        await _unitOfWork.SaveChangesAsync();

        var dto = _mapper.Map<SavedListingDto>(property);
        dto.SavedAt = saved.SavedAt;

        return dto;
    }
}
