using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using RoomRaah.Application.Features.Properties.Commands;
using RoomRaah.UnitTests.Common;

namespace RoomRaah.UnitTests.Properties;

public class UploadPropertyPhotoCommandValidatorTests
{
    private readonly UploadPropertyPhotoCommandValidator _validator =
        new(Options.Create(TestData.PhotoSettings()));

    private static UploadPropertyPhotoCommand Valid() => new()
    {
        PropertyId = 1,
        OwnerId = 1,
        FileName = "room.jpg",
        ContentType = "image/jpeg",
        Length = 120_000,
        Content = new MemoryStream([1, 2, 3])
    };

    [Fact]
    public void A_reasonable_photograph_passes()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void A_request_with_no_file_name_is_refused()
    {
        var command = Valid();
        command.FileName = "";

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.FileName);
    }

    [Fact]
    public void A_file_name_stops_at_200_characters()
    {
        var command = Valid();
        command.FileName = new string('a', 201);

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.FileName);
    }

    [Fact]
    public void An_empty_file_is_refused()
    {
        var command = Valid();
        command.Length = 0;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Length);
    }

    [Fact]
    public void Exactly_five_megabytes_is_still_accepted()
    {
        var command = Valid();
        command.Length = 5 * 1024 * 1024;

        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(x => x.Length);
    }

    [Fact]
    public void A_byte_over_five_megabytes_is_refused()
    {
        var command = Valid();
        command.Length = 5 * 1024 * 1024 + 1;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Length);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("IMAGE/JPEG")]
    public void The_three_accepted_formats_pass_whatever_their_case(string contentType)
    {
        var command = Valid();
        command.ContentType = contentType;

        _validator.TestValidate(command).ShouldNotHaveValidationErrorFor(x => x.ContentType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("application/pdf")]
    [InlineData("image/gif")]
    [InlineData("image/svg+xml")]
    public void Anything_else_is_refused(string contentType)
    {
        var command = Valid();
        command.ContentType = contentType;

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.ContentType);
    }
}
