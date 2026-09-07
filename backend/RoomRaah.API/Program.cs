using System.Text;
using Amazon.S3;
using Azure.Storage.Blobs;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using RoomRaah.API.Middleware;
using RoomRaah.API.Realtime;
using RoomRaah.Application;
using RoomRaah.Application.Authentication;
using RoomRaah.Application.Behaviours;
using RoomRaah.Application.Interfaces;
using RoomRaah.Application.Listings;
using RoomRaah.Application.Services;
using RoomRaah.Infrastructure.Authentication;
using RoomRaah.Infrastructure.BackgroundJobs;
using RoomRaah.Infrastructure.Caching;
using RoomRaah.Infrastructure.Email;
using RoomRaah.Infrastructure.Persistence;
using RoomRaah.Infrastructure.Repositories;
using RoomRaah.Infrastructure.Services;
using RoomRaah.Infrastructure.Storage;
using Serilog;
using StackExchange.Redis;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog (console + rolling daily file) ---
// Providers are cleared so writeToProviders adds nothing in production; it exists so
// a test can attach a provider and assert on what was actually logged.
//
// preserveStaticLogger keeps this host's logger to itself. Left at its default,
// UseSerilog assigns the logger it builds to the process-wide Serilog.Log and then
// resolves every ILogger<T> through that static field at write time — so a second
// host in the same process takes ownership of the first host's logging, and the
// first host's events start arriving in the second one's sinks. Nothing here writes
// through the static Log, so there is nothing to give up by not claiming it.
builder.Logging.ClearProviders();
builder.Host.UseSerilog((context, config) => config
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .WriteTo.Console()
    .WriteTo.File("logs/roomraah-.log", rollingInterval: RollingInterval.Day),
    preserveStaticLogger: true,
    writeToProviders: true);

// --- Database (EF Core + SQL Server) ---
builder.Services.AddDbContext<RoomRaahDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// --- Redis ---
// AbortOnConnectFail stays false so a cold or missing Redis degrades the cache
// instead of preventing the API from starting.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis")!);
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});

// --- Photo storage ---
// Two providers behind one interface, chosen by configuration. §8 gave distance the
// same seam so a paid provider could replace the free one without touching a handler;
// storage has it for the same reason, and an AWS deployment is what uses it.
//
// The default is Azure Blob, which is what docker-compose runs through Azurite. Set
// Storage:Provider to "S3" and nothing above the Infrastructure layer notices.
var storageProvider = builder.Configuration["Storage:Provider"] ?? "AzureBlob";
var usesS3 = string.Equals(storageProvider, "S3", StringComparison.OrdinalIgnoreCase);

builder.Services.Configure<S3StorageSettings>(builder.Configuration.GetSection("S3Storage"));

if (usesS3)
{
    builder.Services.AddSingleton<IAmazonS3>(_ =>
    {
        var settings = builder.Configuration.GetSection("S3Storage").Get<S3StorageSettings>()
                       ?? new S3StorageSettings();

        var config = new AmazonS3Config { ForcePathStyle = settings.ForcePathStyle };

        if (!string.IsNullOrWhiteSpace(settings.ServiceUrl))
        {
            // MinIO or LocalStack. Against AWS itself this is left unset and the SDK
            // resolves the endpoint from the region it was given.
            config.ServiceURL = settings.ServiceUrl;
        }

        // Credentials come from the default chain — an instance role on EC2, or the
        // environment. Nothing reads a key out of configuration, so nothing can put
        // one in a file that gets committed.
        return new AmazonS3Client(config);
    });
}
else
{
    builder.Services.AddSingleton(_ =>
        new BlobServiceClient(builder.Configuration.GetConnectionString("AzureBlob")));
}

// --- Settings ---
builder.Services.Configure<OtpSettings>(builder.Configuration.GetSection("OtpSettings"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<PhotoSettings>(builder.Configuration.GetSection("PhotoSettings"));

// --- Repositories ---
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<IFacilityRepository, FacilityRepository>();
builder.Services.AddScoped<IPropertyRepository, PropertyRepository>();
builder.Services.AddScoped<IPropertyPhotoRepository, PropertyPhotoRepository>();
builder.Services.AddScoped<IPropertyRevisionRepository, PropertyRevisionRepository>();
builder.Services.AddScoped<IVerificationCheckRepository, VerificationCheckRepository>();
builder.Services.AddScoped<ISavedPropertyRepository, SavedPropertyRepository>();
builder.Services.AddScoped<IVisitRequestRepository, VisitRequestRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IInspectionRepository, InspectionRepository>();
builder.Services.AddScoped<IBadgeEventRepository, BadgeEventRepository>();

// --- Services ---
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IAuthSessionService, AuthSessionService>();
if (usesS3)
{
    builder.Services.AddScoped<IPhotoStorageService, S3PhotoStorageService>();
}
else
{
    builder.Services.AddScoped<IPhotoStorageService, AzureBlobPhotoStorageService>();
}

// Stateless and holds nothing, so one instance serves every request. Rule 31 lives
// behind it: swapping this line is what a different distance source would cost.
builder.Services.AddSingleton<IDistanceCalculator, HaversineDistanceCalculator>();

// --- SignalR ---
// The presence tracker is a singleton because it is the process's answer to "who
// is connected", and the notifier is scoped like every other service a handler
// takes. IRealtimeNotifier is declared in Application and implemented here, which
// is what keeps ASP.NET Core out of Infrastructure entirely.
builder.Services.AddSignalR();
builder.Services.AddSingleton<ChatPresence>();
builder.Services.AddScoped<IRealtimeNotifier, RealtimeNotifier>();

// --- Background jobs ---
// Each one is only a clock: it opens a scope on its timer and sends a MediatR command,
// which is where all of the work and all of the tests are. Rule 127.
builder.Services.Configure<BackgroundJobOptions>(builder.Configuration.GetSection("BackgroundJobs"));
builder.Services.AddHostedService<StaleListingSweepService>();
builder.Services.AddHostedService<ExpiredOtpCleanupService>();

// --- MediatR + validation pipeline ---
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(IApplicationAssemblyMarker).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
});

// --- FluentValidation ---
builder.Services.AddValidatorsFromAssembly(typeof(IApplicationAssemblyMarker).Assembly);

// --- AutoMapper ---
builder.Services.AddAutoMapper(cfg => { }, typeof(IApplicationAssemblyMarker).Assembly);

// --- JWT authentication ---
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.Zero
        };

        // A browser cannot set an Authorization header on a WebSocket handshake, so
        // SignalR sends the token as a query parameter instead. This is read for the
        // hub path and for nothing else, so no other endpoint in the product gains a
        // second way to authenticate.
        //
        // §4.2 forbids a token in a log line, and none appears: Serilog's request log
        // writes Request.Path, which does not include the query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(token) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    // Business rule 5: an unverified account may browse, but may take no action
    // that creates a record. Every such endpoint carries this policy.
    options.AddPolicy("VerifiedAccount", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(TokenService.EmailVerifiedClaim, "true"));
});

// --- CORS ---
// The Angular dev server by default, and whatever a deployment names instead. It has
// to be configurable: the frontend is deployed to an origin this file cannot know,
// and an API that only trusts localhost is an API that refuses the only client it has.
//
// Origins are named rather than wildcarded, and not because of taste: SignalR's
// negotiate step sends credentials, and a wildcard origin is not allowed with them.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    allowedOrigins = ["http://localhost:4200"];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// --- Controllers + Swagger/OpenAPI ---
builder.Services.AddControllers();

// [ApiController] answers a model-binding failure itself, before the request reaches
// ExceptionHandlingMiddleware, and it answers in RFC 9110 ProblemDetails whose
// "errors" is a dictionary rather than the list every other error in this API
// returns. One endpoint replying in two shapes is a client bug waiting to happen,
// so binding failures are rewritten into the documented envelope.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ErrorResponse
    {
        Success = false,
        StatusCode = StatusCodes.Status400BadRequest,
        Error = "One or more validation errors occurred.",
        TraceId = context.HttpContext.TraceIdentifier,
        Errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error => new ValidationError
            {
                Field = entry.Key,
                Message = error.ErrorMessage
            }))
            .ToList()
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "RoomRaah API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT access token"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() }
    });

    var apiXml = Path.Combine(AppContext.BaseDirectory, "RoomRaah.API.xml");
    if (File.Exists(apiXml)) options.IncludeXmlComments(apiXml);

    var appXml = Path.Combine(AppContext.BaseDirectory, "RoomRaah.Application.xml");
    if (File.Exists(appXml)) options.IncludeXmlComments(appXml);
});

var app = builder.Build();

// Request logging sits OUTSIDE the exception middleware. Inside it, an exception on
// its way to being turned into a 404 or a 422 is still an exception when the logging
// middleware sees it, so every handled failure was recorded as "responded 500" at
// Error level while the client was correctly receiving its 4xx.
// Request completion events go to this host's own logger. The default is the static
// Serilog.Log, which is process-wide: a second host in the same process replaces it,
// and this host's request log then disappears into the other one's sinks.
app.UseSerilogRequestLogging(options =>
    options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");

// Creating the schema and filling it used to be tied to Development, and a deployment
// found out what that meant: the API came up, answered its port, and every request
// failed on a database that had never been created. The environment was the wrong
// question. A deployment needs a schema on its first boot whatever it calls itself,
// and switching it to "Development" to get one would also switch on Swagger and the
// developer exception page — three unrelated decisions riding on one word.
//
// So each is its own switch, and both default to what Development did before, which
// means nothing changes for anybody running this locally.
var migrateOnStartup = app.Configuration.GetValue(
    "Database:MigrateOnStartup", app.Environment.IsDevelopment());

// Seeding is separate because it is a far bigger claim than migrating: these seeders
// write demo accounts with a known password. A real deployment migrates and does not
// seed, and that combination has to be expressible.
var seedOnStartup = app.Configuration.GetValue(
    "Database:SeedOnStartup", app.Environment.IsDevelopment());

if (migrateOnStartup || seedOnStartup)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<RoomRaahDbContext>();

    if (migrateOnStartup)
    {
        await context.Database.MigrateAsync();
    }

    if (seedOnStartup)
    {
        // Asked to seed a schema that is not there, this throws at startup rather
        // than serving requests against a half-made database. That is the right way
        // round: the failure everybody just paid for was the silent one.
        await ReferenceDataSeeder.SeedAsync(context, scope.ServiceProvider.GetRequiredService<ICacheService>());
        await DataSeeder.SeedAsync(context, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
        await PropertySeeder.SeedAsync(context, scope.ServiceProvider.GetRequiredService<IPhotoStorageService>());
        await SeekerActivitySeeder.SeedAsync(context);
        await ConversationSeeder.SeedAsync(context);
        await InspectionSeeder.SeedAsync(context);
    }
}

app.Run();

public partial class Program
{
}
