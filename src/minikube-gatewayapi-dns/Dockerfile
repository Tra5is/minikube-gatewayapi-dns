# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

# Copy project files
COPY src/minikube-gatewayapi-dns/minikube-gatewayapi-dns.csproj ./minikube-gatewayapi-dns/
COPY src/k8s.GatewayApi.Model/k8s.GatewayApi.Model.csproj ./k8s.GatewayApi.Model/

# Restore dependencies
RUN dotnet restore "./minikube-gatewayapi-dns/minikube-gatewayapi-dns.csproj"

# Copy source code
COPY src/ .

# Build and publish
RUN dotnet publish "./minikube-gatewayapi-dns/minikube-gatewayapi-dns.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine

WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/publish .

EXPOSE 53/udp

ENTRYPOINT ["./minikube-gatewayapi-dns"]
