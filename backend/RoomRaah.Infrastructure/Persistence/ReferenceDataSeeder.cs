using Microsoft.EntityFrameworkCore;
using RoomRaah.Application.Caching;
using RoomRaah.Application.Interfaces;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.Infrastructure.Persistence;

/// <summary>
/// Cities, areas, landmarks and facilities. These are real places with real
/// coordinates: the distance filter, the map and every suggestion list are judged
/// against them, and invented data would make all three impossible to sanity-check.
/// </summary>
public static class ReferenceDataSeeder
{
    public static async Task SeedAsync(RoomRaahDbContext context, ICacheService cache)
    {
        var seededLocations = await SeedLocationsAsync(context);
        var seededFacilities = await SeedFacilitiesAsync(context);

        // There is no endpoint that edits reference data, so nothing else would ever
        // clear these entries before their 24-hour lifetime ran out. Without this a
        // re-seeded database answers from the previous data set for another day.
        if (seededLocations)
        {
            await cache.RemoveByPrefixAsync(CacheKeys.LocationsPrefix);
        }

        if (seededFacilities)
        {
            await cache.RemoveByPrefixAsync(CacheKeys.FacilitiesPrefix);
        }
    }

    private static async Task<bool> SeedLocationsAsync(RoomRaahDbContext context)
    {
        if (await context.Cities.IgnoreQueryFilters().AnyAsync())
        {
            return false;
        }

        var lahore = new City
        {
            Name = "Lahore",
            Areas = Areas("Gulberg", "Johar Town", "Model Town"),
            Landmarks = new List<Landmark>
            {
                Landmark("University of the Punjab", LandmarkKind.University, 31.4966, 74.2996),
                Landmark("UET Lahore", LandmarkKind.University, 31.5786, 74.3568),
                Landmark("LUMS", LandmarkKind.University, 31.4708, 74.4113)
            }
        };

        var karachi = new City
        {
            Name = "Karachi",
            Areas = Areas("Clifton", "Gulshan-e-Iqbal", "North Nazimabad"),
            Landmarks = new List<Landmark>
            {
                Landmark("University of Karachi", LandmarkKind.University, 24.9420, 67.1200),
                Landmark("NED University of Engineering and Technology", LandmarkKind.University, 24.9300, 67.1130),
                Landmark("I. I. Chundrigar Road", LandmarkKind.Workplace, 24.8496, 67.0101)
            }
        };

        var islamabad = new City
        {
            Name = "Islamabad",
            Areas = Areas("F-7", "G-11", "I-8"),
            Landmarks = new List<Landmark>
            {
                Landmark("NUST", LandmarkKind.University, 33.6425, 72.9906),
                Landmark("Blue Area", LandmarkKind.Workplace, 33.7104, 73.0551)
            }
        };

        context.Cities.AddRange(lahore, karachi, islamabad);
        await context.SaveChangesAsync();

        return true;
    }

    private static async Task<bool> SeedFacilitiesAsync(RoomRaahDbContext context)
    {
        if (await context.Facilities.IgnoreQueryFilters().AnyAsync())
        {
            return false;
        }

        context.Facilities.AddRange(
            Facility("Wi-Fi", "wifi", 1),
            Facility("Mess", "mess", 2),
            Facility("Air Conditioning", "ac", 3),
            Facility("Laundry", "laundry", 4),
            Facility("Parking", "parking", 5),
            Facility("Security", "security", 6),
            Facility("Electricity Backup", "power-backup", 7));

        await context.SaveChangesAsync();

        return true;
    }

    private static List<Area> Areas(params string[] names)
    {
        return names.Select(name => new Area { Name = name }).ToList();
    }

    private static Landmark Landmark(string name, LandmarkKind kind, double latitude, double longitude)
    {
        return new Landmark
        {
            Name = name,
            Kind = kind,
            Latitude = latitude,
            Longitude = longitude
        };
    }

    private static Facility Facility(string name, string iconKey, int sortOrder)
    {
        return new Facility { Name = name, IconKey = iconKey, SortOrder = sortOrder };
    }
}
