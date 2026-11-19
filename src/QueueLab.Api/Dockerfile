# Build:  docker build -t queuelab-api -f src/QueueLab.Api/Dockerfile .
# Run:    docker run -p 5000:8080 queuelab-api
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/QueueLab.Core/QueueLab.Core.csproj src/QueueLab.Core/
COPY src/QueueLab.Api/QueueLab.Api.csproj src/QueueLab.Api/
RUN dotnet restore src/QueueLab.Api/QueueLab.Api.csproj
COPY src/QueueLab.Core src/QueueLab.Core
COPY src/QueueLab.Api src/QueueLab.Api
RUN dotnet publish src/QueueLab.Api/QueueLab.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "QueueLab.Api.dll"]
