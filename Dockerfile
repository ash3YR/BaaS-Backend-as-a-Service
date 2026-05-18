# Stage 1: Build the React frontend
FROM node:20 AS frontend-build
WORKDIR /frontend
# Copy package.json and install dependencies
COPY frontend/package*.json ./
RUN npm install
# Copy the rest of the frontend source code
COPY frontend/ .
# Since vite.config.js is configured to output to ../wwwroot, 
# it will place the built files in /wwwroot (relative to /frontend)
RUN npm run build

# Stage 2: Build the .NET application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy the project file and restore any dependencies
COPY ["BaaS.csproj", "./"]
RUN dotnet restore "./BaaS.csproj"

# Copy the rest of the application code
COPY . .

# Copy the built frontend static files from Stage 1 into the .NET wwwroot directory
# We copy them to the source wwwroot so they get included in the publish output
COPY --from=frontend-build /wwwroot ./wwwroot/

# Build the .NET application
RUN dotnet build "./BaaS.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the .NET application
FROM backend-build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BaaS.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 3: Create the final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080

# Copy the published backend (which now includes wwwroot)
COPY --from=publish /app/publish .

# Render dynamically assigns a PORT environment variable.
ENTRYPOINT ["dotnet", "BaaS.dll"]
