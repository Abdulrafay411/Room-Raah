using AutoMapper;
using MediatR;
using RoomRaah.Application.DTOs;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Domain.Exceptions;

namespace RoomRaah.Application.Features.Search.Queries;

public class ComparePropertiesQueryHandler
    : IRequestHandler<ComparePropertiesQuery, List<ListingComparisonDto>>
{
    private readonly IPropertyRepository _properties;
    private readonly ILocationRepository _locations;
    private readonly IDistanceCalculator _distance;
    private readonly IMapper _mapper;

    public ComparePropertiesQueryHandler(
        IPropertyRepository properties,
        ILocationRepository locations,
        IDistanceCalculator distance,
        IMapper mapper)
    {
        _properties = properties;
        _locations = locations;
        _distance = distance;
        _mapper = mapper;
    }

    public async Task<List<ListingComparisonDto>> Handle(
        ComparePropertiesQuery request, CancellationToken cancellationToken)
    {
        IdList.TryParse(request.Ids, out var parsed);

        // Rule 81: duplicates count once, and they are removed before rule 35 counts.
        // Comparing a listing with itself is a mistake in a client, not a request for
        // two of the three slots.
        var ids = parsed.Distinct().ToList();

        if (ids.Count > ComparePropertiesQuery.MaxListings)
        {
            throw new BusinessRuleException(SearchMessages.TooManyToCompare);
        }

        var landmark = await SearchFilters.ResolveLandmarkAsync(_locations, request.LandmarkId);
        var measure = SearchFilters.Measure(_distance, landmark);

        var found = await _properties.GetPublishedManyAsync(ids);

        // Rule 81: the order the ids were given, and an id that resolves to nothing
        // is simply absent. A shortlist outlives the listings on it, and refusing all
        // three because one was unpublished this morning is the least useful answer
        // available — the client can see which of the ids it sent came back.
        return ids
            .Select(id => found.FirstOrDefault(p => p.Id == id))
            .Where(listing => listing is not null)
            .Select(listing =>
            {
                var card = _mapper.Map<ListingComparisonDto>(listing!);
                card.DistanceKm = measure(listing!);
                return card;
            })
            .ToList();
    }
}
