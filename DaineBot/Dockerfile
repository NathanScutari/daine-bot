# Étape de build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DaineBot/DaineBot.csproj ./DaineBot/
COPY DaineBot/ ./

RUN dotnet restore DaineBot/DaineBot.csproj

RUN dotnet publish DaineBot/DaineBot.csproj -c Release -o /app/publish

# Étape de runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DaineBot.dll"]