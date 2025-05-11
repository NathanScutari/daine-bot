# Étape de build : utilise l'image SDK de .NET 8.0 pour la construction
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copie le fichier .csproj et restaure les dépendances
COPY DaineBot/DaineBot.csproj ./DaineBot/
COPY DaineBot/ ./DaineBot/
WORKDIR /src/DaineBot
RUN dotnet restore

# Publie l'application dans le répertoire /app/publish
RUN dotnet publish -c Release -o /app/publish

# Étape de runtime : utilise une image .NET 8.0 pour l'exécution
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Spécifie le point d'entrée de l'application
ENTRYPOINT ["dotnet", "DaineBot.dll"]