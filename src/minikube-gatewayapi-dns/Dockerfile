# Build stage
FROM dhi.io/dotnet:10-sdk AS build

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

# Capability stage — give the apphost binary cap_net_bind_service so the
# non-root runtime user can bind UDP/53 without runtime ambient-cap support.
FROM debian:12-slim AS setcap
RUN apt-get update \
 && apt-get install -y --no-install-recommends libcap2-bin \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish /app/publish
RUN setcap 'cap_net_bind_service=+ep' /app/publish/minikube-gatewayapi-dns

# Runtime stage
FROM dhi.io/dotnet:10

WORKDIR /app

# Copy published application (with file capability preserved via xattr)
COPY --from=setcap /app/publish .

EXPOSE 53/udp
EXPOSE 53/tcp

ENTRYPOINT ["./minikube-gatewayapi-dns"]
