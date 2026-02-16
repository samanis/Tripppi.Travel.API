# Production-ready multi-stage Docker build for AWS ECS Fargate

# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Tripppi.Travel.API/Tripppi.Travel.API.csproj", "Tripppi.Travel.API/"]
RUN dotnet restore "./Tripppi.Travel.API/Tripppi.Travel.API.csproj"
COPY . .
WORKDIR "/src/Tripppi.Travel.API"
RUN dotnet build "./Tripppi.Travel.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Tripppi.Travel.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final production stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Run as non-root user for security
USER $APP_UID

ENTRYPOINT ["dotnet", "Tripppi.Travel.API.dll"]