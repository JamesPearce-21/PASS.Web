# Use Microsoft’s official .NET SDK to build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy only the csproj first and restore dependencies (caching layer)
COPY PASS.Web/PASS.Web.csproj ./PASS.Web.csproj
RUN dotnet restore ./PASS.Web.csproj

# Copy the rest of the project
COPY . ./
RUN dotnet publish ./PASS.Web.csproj -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "PASS.Web.dll"]
