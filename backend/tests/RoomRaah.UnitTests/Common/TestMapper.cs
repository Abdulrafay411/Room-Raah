using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using RoomRaah.Application.Mappings;

namespace RoomRaah.UnitTests.Common;

/// <summary>
/// A real mapper built from the production profiles, so a broken profile fails a
/// handler test instead of hiding behind a substitute. The whole assembly is
/// scanned, exactly as Program.cs does it, so a profile added in a later phase is
/// covered without anyone remembering to list it here.
/// </summary>
public static class TestMapper
{
    public static IMapper Create()
    {
        var configuration = new MapperConfiguration(
            cfg => cfg.AddMaps(typeof(UserProfile).Assembly),
            NullLoggerFactory.Instance);

        return configuration.CreateMapper();
    }
}
