# �tape de build : utilise l'image SDK de .NET 8.0 pour la construction
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copie le fichier .csproj et restaure les d�pendances
COPY DaineBot/DaineBot.csproj ./DaineBot/
COPY DaineBot/ ./DaineBot/
WORKDIR /src/DaineBot
RUN dotnet restore

# Publie l'application dans le r�pertoire /app/publish
RUN dotnet publish -c Release -o /app/publish

# �tape de runtime : utilise une image .NET 8.0 pour l'ex�cution
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Sp�cifie le point d'entr�e de l'application
ENTRYPOINT ["dotnet", "DaineBot.dll"]