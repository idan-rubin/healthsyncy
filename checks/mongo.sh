#!/usr/bin/env bash
set -euo pipefail
export MONGO_INITDB_ROOT_USERNAME=fixture
export MONGO_INITDB_ROOT_PASSWORD=$(openssl rand -hex 24)
export MONGO_SYNC=$(openssl rand -hex 24) MONGO_CONNECTOR=$(openssl rand -hex 24) MONGO_ADMIN=$(openssl rand -hex 24)
# On macOS, .NET uses the system keychain for TLS. Run the fixture in Linux
# so its throwaway issuer stays local to the suite.
linux_fixture=false
if [[ $(uname -s) == Darwin || ${1:-} == --container ]]; then
  linux_fixture=true
  fixture_image=$(docker build --quiet -f checks/Dockerfile .)
fi
container=$(docker run --detach --publish 127.0.0.1::27017 --env MONGO_SYNC --env MONGO_CONNECTOR --env MONGO_ADMIN \
  --env MONGO_INITDB_ROOT_USERNAME --env MONGO_INITDB_ROOT_PASSWORD \
  --volume "$PWD/deploy/k3s/mongo-init.js:/docker-entrypoint-initdb.d/healthsyncy.js:ro" mongo:8.0)
trap 'docker rm -f "$container" >/dev/null' EXIT
port=$(docker port "$container" 27017/tcp | cut -d: -f2)
if $linux_fixture; then port=27017; fi
export TEST_MONGO_URI="mongodb://fixture:$MONGO_INITDB_ROOT_PASSWORD@127.0.0.1:$port/?authSource=admin"
for role in sync connector admin; do
  upper=$(printf %s "$role" | tr a-z A-Z)
  variable="MONGO_$upper"
  export "${upper}_MONGO_URI=mongodb://$role:${!variable}@127.0.0.1:$port/activity?authSource=activity"
done
for i in {1..60}; do
  docker exec "$container" mongosh --quiet --host 127.0.0.1 --username connector --password "$MONGO_CONNECTOR" \
    --authenticationDatabase activity --eval 'db.getSiblingDB("activity").days.findOne()' >/dev/null 2>&1 && break
  sleep 1
done
docker exec "$container" mongosh --quiet --host 127.0.0.1 --username connector --password "$MONGO_CONNECTOR" \
  --authenticationDatabase activity --eval '
    const a = db.getSiblingDB("activity");
    if (a.runCommand({find:"days", filter:{}}).ok !== 1) throw Error("Connector read failed");
    for (const command of [{find:"settings",filter:{}},{insert:"days",documents:[{_id:"forbidden"}]}]) {
      let code;
      try { code = a.runCommand(command).code; } catch (e) { code = e.code; }
      if (code !== 13) throw Error("Expected unauthorized Mongo operation");
    }'
if $linux_fixture; then
  docker run --rm --network "container:$container" \
    --env TEST_MONGO_URI --env SYNC_MONGO_URI --env CONNECTOR_MONGO_URI --env ADMIN_MONGO_URI "$fixture_image"
else
  dotnet run --project checks/HealthSyncy.Checks -c Release
  checks/http.sh
fi
