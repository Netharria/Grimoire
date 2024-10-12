#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Grimoire/Grimoire.csproj", "Grimoire/"]
COPY ["Grimoire/Grimoire.csproj", "Grimoire/"]
COPY ["Grimoire.Domain/Grimoire.Domain.csproj", "Grimoire.Domain/"]
RUN dotnet restore "Grimoire/Grimoire.csproj"
COPY . .
WORKDIR "/src/Grimoire"
RUN dotnet build "Grimoire.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Grimoire.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Grimoire.dll"]
