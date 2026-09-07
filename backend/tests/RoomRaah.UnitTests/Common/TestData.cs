using RoomRaah.Application.Listings;
using RoomRaah.Domain.Entities;
using RoomRaah.Domain.Enums;

namespace RoomRaah.UnitTests.Common;

public static class TestData
{
    public static AppUser User(
        int id = 1,
        string email = "seeker@roomraah.local",
        UserRole role = UserRole.Seeker,
        bool isActive = true,
        bool isEmailVerified = true)
    {
        return new AppUser
        {
            Id = id,
            FullName = "Test Person",
            Email = email,
            PhoneNumber = "+923001112233",
            PasswordHash = "hashed",
            Role = role,
            IsActive = isActive,
            IsEmailVerified = isEmailVerified
        };
    }

    public static City City(int id = 1, string name = "Lahore")
    {
        return new City { Id = id, Name = name };
    }

    public static Area Area(int id = 1, string name = "Gulberg", City? city = null)
    {
        var parent = city ?? City();

        return new Area
        {
            Id = id,
            Name = name,
            CityId = parent.Id,
            City = parent
        };
    }

    public static Landmark Landmark(
        int id = 1,
        string name = "NUST",
        LandmarkKind kind = LandmarkKind.University,
        double latitude = 33.6425,
        double longitude = 72.9906,
        City? city = null)
    {
        var parent = city ?? City(2, "Islamabad");

        return new Landmark
        {
            Id = id,
            Name = name,
            Kind = kind,
            Latitude = latitude,
            Longitude = longitude,
            CityId = parent.Id,
            City = parent
        };
    }

    public static Facility Facility(int id = 1, string name = "Wi-Fi", string iconKey = "wifi", int sortOrder = 1)
    {
        return new Facility { Id = id, Name = name, IconKey = iconKey, SortOrder = sortOrder };
    }

    public static Property Property(
        int id = 1,
        int ownerId = 1,
        PropertyStatus status = PropertyStatus.Draft,
        List<PropertyPhoto>? photos = null,
        Area? area = null,
        int totalBeds = 4,
        int availableBeds = 2)
    {
        var parent = area ?? Area();

        return new Property
        {
            Id = id,
            OwnerId = ownerId,
            AreaId = parent.Id,
            Area = parent,
            Title = "Bright shared room near Main Boulevard",
            Description = "A four-bed shared room in a well-kept portion.",
            AddressLine = "House 42, Block C, Gulberg III",
            Latitude = 31.5204,
            Longitude = 74.3587,
            RoomType = RoomType.Shared,
            GenderPolicy = GenderPolicy.Boys,
            TotalBeds = totalBeds,
            AvailableBeds = availableBeds,
            MonthlyRent = 18000m,
            SecurityDeposit = 18000m,
            UtilitiesCharge = 2500m,
            MessCharge = 8000m,
            Status = status,
            AvailabilityConfirmedAt = DateTime.UtcNow.AddDays(-2),
            Photos = photos ?? []
        };
    }

    /// <summary>A listing an admin can act on: an owner behind it, and its checks.</summary>
    public static Property ReviewableProperty(
        int id = 1,
        int ownerId = 1,
        PropertyStatus status = PropertyStatus.PendingReview,
        bool checksPass = true,
        List<PropertyPhoto>? photos = null)
    {
        var property = Property(id, ownerId, status, photos ?? Photos(3, id));

        property.Owner = User(ownerId, "owner@roomraah.local", UserRole.Owner);
        property.Owner.OwnerProfile = new OwnerProfile
        {
            AppUserId = ownerId,
            CnicNumber = "3520112345671",
            IdentityStatus = VerificationStatus.Checked
        };

        if (checksPass)
        {
            foreach (var type in ListingReview.RequiredToPublish)
            {
                property.VerificationChecks.Add(Check(property.Id, type));
            }
        }

        return property;
    }

    public static VerificationCheck Check(
        int propertyId = 1,
        VerificationCheckType checkType = VerificationCheckType.Address,
        VerificationStatus status = VerificationStatus.Checked)
    {
        return new VerificationCheck
        {
            PropertyId = propertyId,
            CheckType = checkType,
            Status = status,
            RecordedAt = DateTime.UtcNow.AddDays(-1)
        };
    }

    /// <summary>A pending revision holding the listing exactly as it stands.</summary>
    public static PropertyRevision Revision(
        Property property,
        RevisionKind kind = RevisionKind.Content,
        Action<PropertyRevision>? change = null)
    {
        var revision = ListingRevisions.Snapshot(property, kind);
        revision.Id = 500 + property.Id;
        revision.Property = property;

        change?.Invoke(revision);

        return revision;
    }

    public static PropertyPhoto Photo(
        int id = 1,
        int propertyId = 1,
        bool isPrimary = false,
        int sortOrder = 1,
        PhotoStatus status = PhotoStatus.Live)
    {
        return new PropertyPhoto
        {
            Id = id,
            PropertyId = propertyId,
            BlobUrl = $"http://blobs.local/properties/{propertyId}/photo{id}.jpg",
            ThumbnailUrl = $"http://blobs.local/properties/{propertyId}/thumb{id}.jpg",
            IsPrimary = isPrimary,
            SortOrder = sortOrder,
            Status = status
        };
    }

    /// <summary>
    /// Photos numbered 1..count, the first of them primary — the state an owner
    /// reaches by uploading that many and touching nothing else.
    /// </summary>
    public static List<PropertyPhoto> Photos(int count, int propertyId = 1)
    {
        return Enumerable.Range(1, count)
            .Select(i => Photo(i, propertyId, isPrimary: i == 1, sortOrder: i))
            .ToList();
    }

    /// <summary>
    /// The documented photo limits. These are the values a handler is given, not
    /// proof that the deployed configuration carries them — PhotoSettingsTests in
    /// the integration suite reads the real configuration and pins that.
    /// </summary>
    public static PhotoSettings PhotoSettings()
    {
        return new PhotoSettings
        {
            ContainerName = "properties",
            MaxFileSizeBytes = 5 * 1024 * 1024,
            MinPhotosPerListing = 3,
            MaxPhotosPerListing = 15,
            ThumbnailLongEdgePx = 400
        };
    }

    public static OtpCode Otp(
        int appUserId = 1,
        OtpPurpose purpose = OtpPurpose.VerifyEmail,
        int minutesUntilExpiry = 10,
        int attemptCount = 0)
    {
        return new OtpCode
        {
            Id = 1,
            AppUserId = appUserId,
            Purpose = purpose,
            CodeHash = "hashed-code",
            ExpiresAt = DateTime.UtcNow.AddMinutes(minutesUntilExpiry),
            AttemptCount = attemptCount
        };
    }

    /// <summary>
    /// A visit request with the listing, the listing's owner and the seeker attached,
    /// because every handler that touches one needs all three.
    /// </summary>
    public static VisitRequest Visit(
        int id = 1,
        int propertyId = 1,
        int seekerId = 2,
        int ownerId = 1,
        VisitStatus status = VisitStatus.Requested,
        DateTime? preferredAt = null,
        VisitType visitType = VisitType.Physical)
    {
        var property = Property(propertyId, ownerId, PropertyStatus.Published);
        property.Owner = User(ownerId, "owner@roomraah.local", UserRole.Owner);

        return new VisitRequest
        {
            Id = id,
            PropertyId = propertyId,
            Property = property,
            SeekerId = seekerId,
            Seeker = User(seekerId, "seeker@roomraah.local"),
            VisitType = visitType,
            PreferredAt = preferredAt ?? DateTime.UtcNow.AddDays(3),
            Status = status,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
    }

    public static Review Review(
        int id = 1,
        int propertyId = 1,
        int authorId = 2,
        int rating = 4,
        ModerationState state = ModerationState.Pending,
        StayStatus stayStatus = StayStatus.Visited)
    {
        return new Review
        {
            Id = id,
            PropertyId = propertyId,
            Property = Property(propertyId, status: PropertyStatus.Published),
            AuthorId = authorId,
            Author = User(authorId, "seeker@roomraah.local"),
            Rating = rating,
            Comment = "The room matches the photographs and the mess food is good.",
            StayStatus = stayStatus,
            ModerationState = state,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
    }

    public static Report Report(
        int id = 1,
        int propertyId = 1,
        int userId = 2,
        ReportStatus status = ReportStatus.Open,
        ReportReason reason = ReportReason.AlreadyTaken)
    {
        return new Report
        {
            Id = id,
            PropertyId = propertyId,
            Property = Property(propertyId, status: PropertyStatus.Published),
            ReportedByUserId = userId,
            ReportedByUser = User(userId, "seeker@roomraah.local"),
            Reason = reason,
            Details = "The room was taken a month ago but the listing still shows two free beds.",
            Status = status,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
    }

    public static SavedProperty Saved(
        int id = 1,
        int userId = 2,
        int propertyId = 1,
        Property? property = null,
        DateTime? savedAt = null)
    {
        return new SavedProperty
        {
            Id = id,
            UserId = userId,
            PropertyId = propertyId,
            Property = property ?? Property(propertyId, status: PropertyStatus.Published),
            SavedAt = savedAt ?? DateTime.UtcNow.AddDays(-1)
        };
    }

    public static Conversation Conversation(
        int id = 1,
        int propertyId = 1,
        int seekerId = 2,
        int ownerId = 1,
        DateTime? lastMessageAt = null)
    {
        var property = Property(propertyId, ownerId, PropertyStatus.Published);
        property.Owner = User(ownerId, "owner@roomraah.local", UserRole.Owner);

        return new Conversation
        {
            Id = id,
            PropertyId = propertyId,
            Property = property,
            SeekerId = seekerId,
            Seeker = User(seekerId, "seeker@roomraah.local"),
            OwnerId = ownerId,
            Owner = property.Owner,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastMessageAt = lastMessageAt ?? DateTime.UtcNow.AddHours(-1)
        };
    }

    public static Message Message(
        int id = 1,
        int conversationId = 1,
        int senderId = 2,
        string body = "Is the room still free?",
        DateTime? sentAt = null,
        DateTime? readAt = null)
    {
        return new Message
        {
            Id = id,
            ConversationId = conversationId,
            SenderId = senderId,
            Sender = User(senderId, "seeker@roomraah.local"),
            Body = body,
            SentAt = sentAt ?? DateTime.UtcNow.AddMinutes(-10),
            ReadAt = readAt
        };
    }

    public static RefreshToken Token(
        int appUserId = 1,
        string token = "refresh-token",
        int daysUntilExpiry = 7,
        DateTime? revokedAt = null)
    {
        return new RefreshToken
        {
            Id = 1,
            AppUserId = appUserId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(daysUntilExpiry),
            RevokedAt = revokedAt
        };
    }
}
