FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/backend/CivicTrace.Domain/CivicTrace.Domain.csproj src/backend/CivicTrace.Domain/
COPY src/backend/CivicTrace.Application/CivicTrace.Application.csproj src/backend/CivicTrace.Application/
COPY src/backend/CivicTrace.Persistence/CivicTrace.Persistence.csproj src/backend/CivicTrace.Persistence/
COPY src/backend/CivicTrace.WebApi/CivicTrace.WebApi.csproj src/backend/CivicTrace.WebApi/
RUN dotnet restore src/backend/CivicTrace.WebApi/CivicTrace.WebApi.csproj

COPY src/backend/ src/backend/
RUN dotnet publish src/backend/CivicTrace.WebApi/CivicTrace.WebApi.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

# Усі дані (listProzorroSales.csv, risk-rules.json, procurements.seed.json)
COPY data/ ./data/

# Фронтенд
COPY src/frontend/ ./frontend/
COPY data/procurements.seed.json ./frontend/procurements.seed.json

RUN mkdir -p /app/logs && chown "$APP_UID" /app/logs
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CivicTrace.WebApi.dll"]