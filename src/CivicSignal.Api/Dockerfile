FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src

COPY Directory.Packages.props global.json ./
COPY src/CivicSignal.Domain/CivicSignal.Domain.csproj src/CivicSignal.Domain/
COPY src/CivicSignal.Application/CivicSignal.Application.csproj src/CivicSignal.Application/
COPY src/CivicSignal.Infrastructure/CivicSignal.Infrastructure.csproj src/CivicSignal.Infrastructure/
COPY src/CivicSignal.Api/CivicSignal.Api.csproj src/CivicSignal.Api/

RUN dotnet restore src/CivicSignal.Api/CivicSignal.Api.csproj

COPY src ./src
RUN dotnet publish src/CivicSignal.Api/CivicSignal.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CivicSignal.Api.dll"]
