FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Install Node.js for Vite build
RUN apt-get update && apt-get install -y nodejs npm

# Copy project files
COPY CustomniMebli.Server/*.csproj ./CustomniMebli.Server/
COPY customnimebli.client/*.esproj ./customnimebli.client/
COPY customnimebli.client/package*.json ./customnimebli.client/

# Restore dependencies
WORKDIR /src/CustomniMebli.Server
RUN dotnet restore

# Copy all source
WORKDIR /src
COPY . .

# Build client
WORKDIR /src/customnimebli.client
RUN npm install
RUN npm run build

# Build server
WORKDIR /src/CustomniMebli.Server
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /src/customnimebli.client/dist ./wwwroot

# Create directories
RUN mkdir -p wwwroot/images/portfolio

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "CustomniMebli.Server.dll"]
