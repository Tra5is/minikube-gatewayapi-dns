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

# Runtime stage
FROM dhi.io/dotnet:10

WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/publish .

EXPOSE 53/udp

ENTRYPOINT ["./minikube-gatewayapi-dns"]
