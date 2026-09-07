using System.Reflection;
using RoomRaah.Application;
using RoomRaah.Application.DTOs;
using Shouldly;

namespace RoomRaah.UnitTests.Privacy;

/// <summary>
/// Business rule 33: no response anywhere returns an owner's phone number, WhatsApp
/// number or email address. These tests run over every DTO by reflection, so a DTO
/// added in a later phase is covered the moment it is written.
/// </summary>
public class DtoPrivacyTests
{
    private static readonly string[] ForbiddenFragments =
    [
        "phone", "whatsapp", "mobile", "cnic", "passwordhash", "payout"
    ];

    /// <summary>
    /// The only DTOs allowed to carry an email address, each with the reason it may.
    /// Adding a type here is a deliberate act, and the reason has to survive review.
    /// </summary>
    private static readonly Dictionary<Type, string> EmailAllowList = new()
    {
        [typeof(UserSummaryDto)] = "the signed-in person's own account, never another person's",
        [typeof(AdminUserSummaryDto)] = "the admin user list; an admin managing accounts needs to tell them apart",
        [typeof(AdminUserDto)] = "the admin user detail, for the same reason"
    };

    /// <summary>
    /// The single exception to rule 33 in the product, named rather than skipped.
    ///
    /// An admin recording the OwnerIdentity verification check is being asked to
    /// confirm an identity, and rule 33 as originally written asked them to confirm it
    /// without being shown it. The exception is one type, and the test below is what
    /// keeps it one type — the *list* shape is deliberately not on here.
    /// </summary>
    private static readonly Dictionary<Type, string> ContactAllowList = new()
    {
        [typeof(AdminUserDto)] = "rule 116: Admin-only, one account at a time, so an identity check can be made"
    };

    private static IEnumerable<Type> AllDtos()
    {
        return typeof(IApplicationAssemblyMarker).Assembly
            .GetTypes()
            .Where(t => t.IsClass && t.IsPublic && t.Name.EndsWith("Dto"));
    }

    public static TheoryData<Type> Dtos()
    {
        var data = new TheoryData<Type>();

        foreach (var type in AllDtos())
        {
            data.Add(type);
        }

        return data;
    }

    [Fact]
    public void There_is_at_least_one_dto_to_check()
    {
        AllDtos().ShouldNotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Dtos))]
    public void Rule_33_no_dto_carries_a_phone_number_or_other_private_contact_detail(Type dto)
    {
        var offenders = dto.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => ForbiddenFragments.Any(
                fragment => p.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .Select(p => p.Name)
            .ToList();

        if (ContactAllowList.ContainsKey(dto))
        {
            return;
        }

        offenders.ShouldBeEmpty(
            $"{dto.Name} exposes {string.Join(", ", offenders)}. " +
            "Business rule 33 forbids a contact detail in any response.");
    }

    /// <summary>
    /// Rule 117, and the reason the exception above stays narrow.
    ///
    /// An allow list only records that somebody decided something; this asserts that
    /// the decision is still the narrow one. The summary shape must never gain a phone
    /// number or a CNIC — a page of fifty accounts is fifty identity numbers on one
    /// screen, in one screenshot and in one browser cache, for a question the list is
    /// not being asked.
    /// </summary>
    [Fact]
    public void Rule_117_the_admin_user_list_carries_no_phone_number_and_no_cnic()
    {
        var names = typeof(AdminUserSummaryDto)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        names.ShouldNotContain("PhoneNumber");
        names.ShouldNotContain("CnicNumber");
    }

    /// <summary>
    /// And the exception is exactly one type. If a second ever appears here, it is a
    /// decision somebody has to make on purpose rather than one that arrives with a
    /// DTO that happened to inherit from the wrong base.
    /// </summary>
    [Fact]
    public void Rule_116_only_one_shape_in_the_product_carries_a_contact_detail()
    {
        ContactAllowList.Keys.ShouldBe([typeof(AdminUserDto)]);
    }

    [Theory]
    [MemberData(nameof(Dtos))]
    public void Rule_33_an_email_address_appears_only_where_it_is_explicitly_allowed(Type dto)
    {
        var carriesEmail = dto.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => p.Name.Contains("email", StringComparison.OrdinalIgnoreCase));

        if (!carriesEmail)
        {
            return;
        }

        EmailAllowList.ContainsKey(dto).ShouldBeTrue(
            $"{dto.Name} exposes an email address but is not on the allow list. " +
            "Either remove the property or add the type with the reason it is safe.");
    }
}
