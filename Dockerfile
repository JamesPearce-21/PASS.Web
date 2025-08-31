# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy csproj and restore dependencies
COPY PASS.Web/PASS.Web.csproj ./PASS.Web.csproj
RUN dotnet restore ./PASS.Web.csproj

# Copy everything else
COPY . ./
RUN dotnet publish ./PASS.Web.csproj -c Release -o /app/out \
    /p:PublishTrimmed=true /p:MvcRazorCompileOnPublish=true

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "PASS.Web.dll"]
