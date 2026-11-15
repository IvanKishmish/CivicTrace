# syntax=docker/dockerfile:1
#
# Збірка ЗАВЖДИ з кореня репозиторію (контекст = корінь), бо потрібні сусідні проєкти, data/ і src/frontend:
#   docker build -f src/backend/CivicTrace.WebApi/Dockerfile -t civictrace-api .
# Усі шляхи в COPY нижче — від кореня репозиторію, а не від теки з цим файлом.

# ---------------------------------------------------------------- build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1) Спершу лише файли проєктів: шар restore не збивається, поки не змінилися пакети
#    (файл .slnx для збірки не потрібен: publish іде за .csproj; restore підтягує й сусідні проєкти за посиланнями)
COPY src/backend/CivicTrace.Domain/CivicTrace.Domain.csproj src/backend/CivicTrace.Domain/
COPY src/backend/CivicTrace.Application/CivicTrace.Application.csproj src/backend/CivicTrace.Application/
COPY src/backend/CivicTrace.Persistence/CivicTrace.Persistence.csproj src/backend/CivicTrace.Persistence/
COPY src/backend/CivicTrace.WebApi/CivicTrace.WebApi.csproj src/backend/CivicTrace.WebApi/
RUN dotnet restore src/backend/CivicTrace.WebApi/CivicTrace.WebApi.csproj

# 2) Потім увесь код і публікація
COPY src/backend/ src/backend/
RUN dotnet publish src/backend/CivicTrace.WebApi/CivicTrace.WebApi.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------------------------------------------------------------- runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

# Демо-дані для імпорту при старті (Seed__ProcurementsPath=/app/data/procurements.seed.json)
COPY data/procurements.seed.json ./data/procurements.seed.json

# Фронтенд: API може віддавати його сам (Frontend__RootPath=/app/frontend).
# Сид-файл поруч із index.html потрібен резервному режиму фронтенду без API (staticUrl у js/config.js).
COPY src/frontend/ ./frontend/
COPY data/procurements.seed.json ./frontend/procurements.seed.json

# Тека логів, доступна для непривілейованого користувача
RUN mkdir -p /app/logs && chown "$APP_UID" /app/logs

# Не від root (користувач app, UID у змінній APP_UID, є в образах .NET 8+)
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CivicTrace.WebApi.dll"]
