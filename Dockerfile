# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY QuotesAggregator.sln ./
COPY Quotes.Api/Quotes.Api.csproj Quotes.Api/
COPY Quotes.Api.Tests/Quotes.Api.Tests.csproj Quotes.Api.Tests/

RUN dotnet restore

COPY . .
WORKDIR /src/Quotes.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Quotes.Api.dll"]