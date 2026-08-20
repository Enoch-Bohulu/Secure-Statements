# ---- Build stage: compile and publish the app ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything, then restore/publish only the API project. Restore follows the API's
# project references, so Domain/Application/Infrastructure are pulled in automatically while
# the test projects are ignored (see .dockerignore) — smaller, faster, deterministic builds.
COPY . .
RUN dotnet restore src/SecureStatements.Api/SecureStatements.Api.csproj
RUN dotnet publish src/SecureStatements.Api/SecureStatements.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage: a small image that just runs the published app ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Run as the non-root 'app' user that ships in the .NET 8 images (least privilege).
# Create the blob directory up front and hand ownership to that user so an empty named
# volume mounted here inherits writable ownership.
RUN mkdir -p /app/data/statements && chown -R app:app /app
USER app

# The app listens on 8080 inside the container
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SecureStatements.Api.dll"]

