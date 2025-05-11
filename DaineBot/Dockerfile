# Étape de build : utilise l'image SDK de .NET 8.0 pour la construction
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copie tous les fichiers nécessaires pour la compilation
COPY DaineBot/DaineBot.csproj ./DaineBot/
COPY DaineBot/ ./

# Restaure les dépendances du projet
RUN dotnet restore DaineBot/DaineBot.csproj

# Publie l'application dans le répertoire /app/publish
RUN dotnet publish DaineBot/DaineBot.csproj -c Release -o /app/publish

# Étape de runtime : utilise une image .NET 8.0 pour l'exécution
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Démarre l'application
ENTRYPOINT ["dotnet", "DaineBot.dll"]
