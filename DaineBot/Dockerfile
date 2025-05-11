# Build stage
FROM mcr.microsoft.comdotnetsdk8.0 AS build
WORKDIR src

COPY . .
RUN dotnet restore DaineBot.sln
RUN dotnet publish DaineBotDaineBot.csproj -c Release -o apppublish

# Runtime stage
FROM mcr.microsoft.comdotnetaspnet8.0
WORKDIR app
COPY --from=build apppublish .
ENTRYPOINT [dotnet, DaineBot.dll]
