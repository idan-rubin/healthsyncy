FROM mcr.microsoft.com/dotnet/sdk:11.0
RUN apt-get update && apt-get install -y --no-install-recommends curl openssl python3 && rm -rf /var/lib/apt/lists/*
WORKDIR /work
COPY Directory.Build.props HealthSyncy.slnx ./
COPY src/ src/
COPY checks/ checks/
RUN dotnet build HealthSyncy.slnx -c Release && dotnet build checks/HealthSyncy.Checks -c Release
CMD ["bash", "-ec", "dotnet run --project checks/HealthSyncy.Checks -c Release --no-build && checks/http.sh"]
