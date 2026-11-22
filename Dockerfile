# Stage 1: Build React frontend
FROM node:24-alpine AS frontend-build
WORKDIR /app/web
COPY src/DocumentProcessing.Web/package*.json ./
RUN npm ci
COPY src/DocumentProcessing.Web/ ./
RUN npm run build

# Stage 2: Build .NET backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY *.slnx Directory.Build.props ./
COPY DocumentProcessing.AppHost/DocumentProcessing.AppHost.csproj DocumentProcessing.AppHost/
COPY DocumentProcessing.ServiceDefaults/DocumentProcessing.ServiceDefaults.csproj DocumentProcessing.ServiceDefaults/
COPY src/DocumentProcessing.Api/DocumentProcessing.Api.csproj src/DocumentProcessing.Api/
COPY src/DocumentProcessing.Core/DocumentProcessing.Core.csproj src/DocumentProcessing.Core/
COPY src/DocumentProcessing.Infrastructure/DocumentProcessing.Infrastructure.csproj src/DocumentProcessing.Infrastructure/
COPY src/DocumentProcessing.Worker/DocumentProcessing.Worker.csproj src/DocumentProcessing.Worker/
COPY tests/DocumentProcessing.Tests/DocumentProcessing.Tests.csproj tests/DocumentProcessing.Tests/
RUN dotnet restore src/DocumentProcessing.Api/DocumentProcessing.Api.csproj

COPY . .
RUN dotnet publish src/DocumentProcessing.Api/DocumentProcessing.Api.csproj -c Release -o /app/publish --no-restore -p:ErrorOnDuplicatePublishOutputFiles=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Install Tesseract OCR and language data
RUN apt-get update && \
    apt-get install -y --no-install-recommends tesseract-ocr tesseract-ocr-eng && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=backend-build /app/publish .
COPY --from=frontend-build /app/web/dist ./wwwroot

# Tesseract data path
ENV Tesseract__TessDataPath=/usr/share/tesseract-ocr/5/tessdata
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "DocumentProcessing.Api.dll"]
