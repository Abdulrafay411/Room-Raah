using AutoMapper;
using MediatR;
using RoomRaah.Application.Caching;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.AdminListings.Commands;

public class RecordVerificationCheckCommandHandler
    : IRequestHandler<RecordVerificationCheckCommand, List<VerificationCheckDto>>
{
    private readonly IPropertyRepository _properties;
    private readonly IVerificationCheckRepository _checks;
    private readonly ICacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public RecordVerificationCheckCommandHandler(
        IPropertyRepository properties,
        IVerificationCheckRepository checks,
        ICacheService cache,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _properties = properties;
        _checks = checks;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<List<VerificationCheckDto>> Handle(
        RecordVerificationCheckCommand request, CancellationToken cancellationToken)
    {
        var property = await _properties.GetForReviewAsync(request.PropertyId)
            ?? throw new NotFoundException(PropertyMessages.PropertyNotFound(request.PropertyId));

        var checkType = Enum.Parse<VerificationCheckType>(request.CheckType, ignoreCase: true);
        var status = Enum.Parse<VerificationStatus>(request.Status, ignoreCase: true);

        // Rule 64. One row per listing per type: an admin who was wrong should be
        // able to correct themselves, and rule 23 has to be able to ask each check a
        // single question rather than reconcile a stack of opinions.
        var check = property.VerificationChecks.FirstOrDefault(c => c.CheckType == checkType);

        if (check is null)
        {
            check = new VerificationCheck
            {
                PropertyId = property.Id,
                CheckType = checkType
            };

            property.VerificationChecks.Add(check);
            await _checks.CreateAsync(check);
        }

        check.Status = status;
        check.EvidenceDate = request.EvidenceDate;
        check.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        check.ReviewedByAdminId = request.AdminId;
        check.RecordedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        // Rule 71. A check that has just failed can change what a published listing
        // is allowed to claim about itself.
        await _cache.RemoveByPrefixAsync(CacheKeys.PropertiesPrefix);

        return _mapper.Map<List<VerificationCheckDto>>(
            property.VerificationChecks.OrderBy(c => c.CheckType).ToList());
    }
}
