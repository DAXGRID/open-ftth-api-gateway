FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

COPY ./*sln ./

COPY ./OpenFTTH.APIGateway/*.csproj ./OpenFTTH.APIGateway/
COPY ./OpenFTTH.APIGateway.IntegrationTests/*.csproj ./OpenFTTH.APIGateway.IntegrationTests/
COPY ./OpenFTTH.RouteNetwork.API/*.csproj ./OpenFTTH.RouteNetwork.API/
COPY ./OpenFTTH.RouteNetwork.Business/*.csproj ./OpenFTTH.RouteNetwork.Business/
COPY ./OpenFTTH.RouteNetwork.Service/*.csproj ./OpenFTTH.RouteNetwork.Service/
COPY ./OpenFTTH.RouteNetwork.Tests/*.csproj ./OpenFTTH.RouteNetwork.Tests/
COPY ./OpenFTTH.Schematic.API/*.csproj ./OpenFTTH.Schematic.API/
COPY ./OpenFTTH.Schematic.Business/*.csproj ./OpenFTTH.Schematic.Business/
COPY ./OpenFTTH.Schematic.Service/*.csproj ./OpenFTTH.Schematic.Service/
COPY ./OpenFTTH.Schematic.Tests/*.csproj ./OpenFTTH.Schematic.Tests/
COPY ./OpenFTTH.UtilityGraphService.API/*.csproj ./OpenFTTH.UtilityGraphService.API/
COPY ./OpenFTTH.UtilityGraphService.Business/*.csproj ./OpenFTTH.UtilityGraphService.Business/
COPY ./OpenFTTH.UtilityGraphService.Service/*.csproj ./OpenFTTH.UtilityGraphService.Service/
COPY ./OpenFTTH.UtilityGraphService.Tests/*.csproj ./OpenFTTH.UtilityGraphService.Tests/

RUN dotnet restore --packages ./packages

COPY . ./

WORKDIR /app/OpenFTTH.APIGateway

RUN dotnet publish -c Release -o out --packages ./packages

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build-env /app/OpenFTTH.APIGateway/out .
ENTRYPOINT ["dotnet", "OpenFTTH.APIGateway.dll"]

ENV ASPNETCORE_URLS=http://+80
EXPOSE 80