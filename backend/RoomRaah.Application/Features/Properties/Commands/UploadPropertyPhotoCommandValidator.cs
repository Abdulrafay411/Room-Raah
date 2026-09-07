using FluentValidation;
using Microsoft.Extensions.Options;
using RoomRaah.Application.Listings;

namespace RoomRaah.Application.Features.Properties.Commands;

public class UploadPropertyPhotoCommandValidator : AbstractValidator<UploadPropertyPhotoCommand>
{
    public UploadPropertyPhotoCommandValidator(IOptions<PhotoSettings> photoSettings)
    {
        var settings = photoSettings.Value;
        var megabytes = settings.MaxFileSizeBytes / (1024d * 1024d);

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("A file is required.")
            .MaximumLength(200).WithMessage("File name must not exceed 200 characters.");

        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage("The file is empty.")
            .LessThanOrEqualTo(settings.MaxFileSizeBytes)
            .WithMessage($"A photo must be {megabytes:0.#} MB or smaller.");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("Content type is required.")
            .Must(BeAnAcceptedType)
            .WithMessage($"A photo must be one of: {string.Join(", ", PhotoSettings.AcceptedContentTypes)}.");
    }

    private static bool BeAnAcceptedType(string contentType)
    {
        return PhotoSettings.AcceptedContentTypes.Contains(
            contentType?.Trim().ToLowerInvariant() ?? string.Empty);
    }
}
