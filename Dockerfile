FROM registry.access.redhat.com/ubi8/dotnet-90:latest  AS base
WORKDIR /app

FROM registry.access.redhat.com/ubi8/dotnet-90:latest AS build
USER root
WORKDIR /src

# Copy project file first for better Docker layer caching
COPY ["DemoWebApplication/DemoWebApplication/DemoWebApplication.csproj", "DemoWebApplication/DemoWebApplication/"]
RUN dotnet restore "DemoWebApplication/DemoWebApplication/DemoWebApplication.csproj"

# Copy all source code
COPY . .
WORKDIR "/src/DemoWebApplication/DemoWebApplication"
RUN dotnet build "DemoWebApplication.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DemoWebApplication.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create directories for certificates and logs with OpenShift compatible permissions
USER root
RUN mkdir -p /app/certs /app/logs && \
    chgrp -R 0 /app && \
    chmod -R g+rw /app

# OpenShift will automatically assign a UID, we just ensure group permissions are correct
# Don't specify USER - let OpenShift handle it

# Expose both HTTP and HTTPS ports
EXPOSE 8080 8443

# Set environment variables for OpenShift/Kubernetes
ENV ASPNETCORE_URLS="https://+:8443;http://+:8080"
ENV ASPNETCORE_HTTPS_PORT=8443
ENV DOTNET_RUNNING_IN_CONTAINER=true


ENTRYPOINT ["dotnet", "DemoWebApplication.dll"]