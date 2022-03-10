FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

COPY ./*sln ./
COPY ./OpenFTTH.APIGateway/*.csproj ./OpenFTTH.APIGateway/
COPY ./OpenFTTH.APIGateway.IntegrationTests/*.csproj ./OpenFTTH.APIGateway.IntegrationTests/
COPY ./OpenFTTH.Work.API/*.csproj ./OpenFTTH.Work.API/
COPY ./OpenFTTH.Work.Business/*.csproj ./OpenFTTH.Work.Business/
COPY ./OpenFTTH.Work.Tests/*.csproj ./OpenFTTH.Work.Tests/

RUN dotnet restore --packages ./packages

COPY . ./
WORKDIR /app/OpenFTTH.APIGateway
RUN dotnet publish -c Release -o out --packages ./packages

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app

COPY --from=build-env /app/OpenFTTH.APIGateway/out .
ENTRYPOINT ["dotnet", "OpenFTTH.APIGateway.dll"]

ENV ASPNETCORE_URLS=http://+80
EXPOSE 80