# RoomRaah API.
#
# Multi-stage: the SDK builds, and only the runtime ships. The published image carries
# no compiler, no source and no NuGet cache — a smaller image and a smaller thing to
# have to trust on a server.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The project files first, so a code change does not invalidate the restore layer.
COPY RoomRaah.slnx ./
COPY RoomRaah.Domain/*.csproj RoomRaah.Domain/
COPY RoomRaah.Application/*.csproj RoomRaah.Application/
COPY RoomRaah.Infrastructure/*.csproj RoomRaah.Infrastructure/
COPY RoomRaah.API/*.csproj RoomRaah.API/

RUN dotnet restore RoomRaah.API/RoomRaah.API.csproj

COPY RoomRaah.Domain/ RoomRaah.Domain/
COPY RoomRaah.Application/ RoomRaah.Application/
COPY RoomRaah.Infrastructure/ RoomRaah.Infrastructure/
COPY RoomRaah.API/ RoomRaah.API/

RUN dotnet publish RoomRaah.API/RoomRaah.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# SkiaSharp needs a font manager and its native dependencies. Without these the
# thumbnail encoder throws at the first upload rather than at startup, which is the
# worst time to find out.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

# Not root. The API writes only to logs/, which is a volume in compose.
RUN useradd --uid 10001 --create-home roomraah \
    && mkdir -p /app/logs \
    && chown -R roomraah:roomraah /app
USER roomraah

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "RoomRaah.API.dll"]
