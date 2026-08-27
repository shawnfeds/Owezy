# Multi-stage Dockerfile for Owezy API (.NET 10)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and csproj files for layer caching
COPY Owezy.slnx ./
COPY src/Owezy.Domain/Owezy.Domain.csproj src/Owezy.Domain/
COPY src/Owezy.Application/Owezy.Application.csproj src/Owezy.Application/
COPY src/Owezy.Infrastructure/Owezy.Infrastructure.csproj src/Owezy.Infrastructure/
COPY src/Owezy.Api/Owezy.Api.csproj src/Owezy.Api/

RUN dotnet restore src/Owezy.Api/Owezy.Api.csproj

# Copy remaining source code and publish
COPY src/ src/
RUN dotnet publish src/Owezy.Api/Owezy.Api.csproj -c Release -o /app/publish --no-restore
RUN cp -r src/Owezy.Client /app/publish/Owezy.Client

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install native libleptonica/libtesseract dependencies for Tesseract OCR (Linux)
RUN apt-get update && apt-get install -y --no-install-recommends \
    tesseract-ocr \
    libleptonica-dev \
    libtesseract-dev \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Persistent storage location for receipt images
ENV RECEIPT_STORAGE_ROOT=/app/receipts
RUN mkdir -p /app/receipts

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Owezy.Api.dll"]
