FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["DBot.csproj", "."]
RUN dotnet restore "./DBot.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "DBot.csproj" -c Release -o /app/build -r linux-x64 --no-self-contained /p:UseAppHost=false

FROM build AS publish
RUN dotnet publish "DBot.csproj" -c Release -o /app/publish -r linux-x64 --no-self-contained /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DBot.dll"]