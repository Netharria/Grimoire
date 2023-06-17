#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Grimoire.Discord/Grimoire.Discord.csproj", "Grimoire.Discord/"]
COPY ["Grimoire.Core/Grimoire.Core.csproj", "Grimoire.Core/"]
COPY ["Grimoire.Domain/Grimoire.Domain.csproj", "Grimoire.Domain/"]
RUN dotnet restore "Grimoire.Discord/Grimoire.Discord.csproj"
COPY . .
WORKDIR "/src/Grimoire.Discord"
RUN dotnet build "Grimoire.Discord.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Grimoire.Discord.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Grimoire.Discord.dll"]