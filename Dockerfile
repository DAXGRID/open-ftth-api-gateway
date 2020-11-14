FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env
WORKDIR /app

COPY ./*sln ./

COPY ./OpenFTTH.APIGateway/*.csproj ./OpenFTTH.APIGateway/
COPY ./OpenFTTH.APIGateway.Remote/*.csproj ./OpenFTTH.APIGateway.Remote/
COPY ./OpenFTTH.APIGateway.CoreTypes/*.csproj ./OpenFTTH.APIGateway.CoreTypes/
COPY ./OpenFTTH.APIGateway.RouteNetwork/*.csproj ./OpenFTTH.APIGateway.RouteNetwork/
COPY ./OpenFTTH.APIGateway.GeographicalAreaUpdated/*.csproj ./OpenFTTH.APIGateway.GeographicalAreaUpdated/
COPY ./OpenFTTH.APIGateway.Work/*.csproj ./OpenFTTH.APIGateway.Work/
COPY ./OpenFTTH.WorkService/*.csproj ./OpenFTTH.WorkService/
COPY ./OpenFTTH.WorkService.API/*.csproj ./OpenFTTH.WorkService.API/

COPY ./OpenFTTH.WorkService.Tests/*.csproj ./OpenFTTH.WorkService.Tests/

RUN dotnet restore --packages ./packages

COPY . ./
WORKDIR /app/OpenFTTH.APIGateway
RUN dotnet publish -c Release -o out --packages ./packages

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /app

COPY --from=build-env /app/OpenFTTH.APIGateway/out .
ENTRYPOINT ["dotnet", "OpenFTTH.APIGateway.dll"]

ENV ASPNETCORE_URLS=https://+443;http://+80
ENV ASPNETCORE_HTTPS_PORT=443
EXPOSE 80
