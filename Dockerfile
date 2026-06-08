# =========================
# Build Stage
# =========================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

# Copy csproj terlebih dahulu untuk cache restore
COPY src/SS.PaymentService.API/*.csproj src/SS.PaymentService.API/

RUN dotnet restore src/SS.PaymentService.API/SS.PaymentService.API.csproj

# Copy seluruh source
COPY . .

# Publish langsung (tidak perlu build terpisah)
RUN dotnet publish \
    src/SS.PaymentService.API/SS.PaymentService.API.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# =========================
# Runtime Stage
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8084

ENV ASPNETCORE_URLS=http://+:8084

ENTRYPOINT ["dotnet", "SS.PaymentService.API.dll"]
