FROM mcr.microsoft.com/dotnet/sdk:11.0
RUN apt-get update && apt-get install -y --no-install-recommends curl openssl python3 && rm -rf /var/lib/apt/lists/*
WORKDIR /work
COPY Directory.Build.props .editorconfig .globalconfig ./
COPY src/ src/
COPY checks/ checks/
RUN for app in checks/HealthSyncy.Checks src/HealthSyncy.Sync/HealthSyncy.Sync.EntryPoint \
      src/HealthSyncy.Connector/HealthSyncy.Connector.EntryPoint src/HealthSyncy.Admin/HealthSyncy.Admin.EntryPoint; do \
      dotnet build "$app" -c Release || exit 1; done
CMD ["bash", "-ec", "dotnet run --project checks/HealthSyncy.Checks -c Release --no-build && checks/http.sh"]
