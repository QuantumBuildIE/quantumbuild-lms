FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and all project files
COPY *.sln ./
COPY src/ ./src/

# Restore (with RID to include platform-specific native assets like PDFium, SkiaSharp)
RUN dotnet restore src/QuantumBuild.API/QuantumBuild.API.csproj -r linux-x64

# Build and publish (framework-dependent for linux-x64)
RUN dotnet publish src/QuantumBuild.API/QuantumBuild.API.csproj -c Release -o /app/publish --no-restore -r linux-x64 --self-contained false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "QuantumBuild.API.dll"]
