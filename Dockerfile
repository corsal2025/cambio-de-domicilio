# Build + test environment (Linux container)
# Use when you don't have .NET SDK locally.
#
# Commands:
#   docker compose run --rm build    # build the project
#   docker compose run --rm test     # run tests
#   docker compose run --rm publish  # publish single-file exe

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet build -c Release --no-restore

FROM build AS test
RUN dotnet test -c Release --no-build --verbosity normal

FROM build AS publish
# A runtime-specific publish needs its own restore, so no --no-build/--no-restore here.
RUN dotnet publish src/CambioDeDomicilio -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o /app/publish
