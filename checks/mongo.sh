#!/usr/bin/env bash
set -euo pipefail
export MONGO_INITDB_ROOT_USERNAME=fixture
export MONGO_INITDB_ROOT_PASSWORD=$(openssl rand -hex 24)
export MONGO_SYNC=$(openssl rand -hex 24) MONGO_CONNECTOR=$(openssl rand -hex 24) MONGO_ADMIN=$(openssl rand -hex 24)
container=$(docker run --detach --publish 127.0.0.1::27017 --env MONGO_SYNC --env MONGO_CONNECTOR --env MONGO_ADMIN \
  --env MONGO_INITDB_ROOT_USERNAME --env MONGO_INITDB_ROOT_PASSWORD \
  --volume "$PWD/deploy/k3s/mongo-init.js:/docker-entrypoint-initdb.d/healthsynki.js:ro" mongo:8.0)
trap 'docker rm -f "$container" >/dev/null' EXIT
port=$(docker port "$container" 27017/tcp | cut -d: -f2)
export TEST_MONGO_URI="mongodb://fixture:$MONGO_INITDB_ROOT_PASSWORD@127.0.0.1:$port/?authSource=admin"
for role in sync connector admin; do
  variable="MONGO_${role^^}"
  export "${role^^}_MONGO_URI=mongodb://$role:${!variable}@127.0.0.1:$port/activity?authSource=activity"
done
for i in {1..60}; do
  docker exec "$container" mongosh --quiet --host 127.0.0.1 --username connector --password "$MONGO_CONNECTOR" \
    --authenticationDatabase activity --eval 'db.getSiblingDB("activity").days.findOne()' >/dev/null 2>&1 && break
  sleep 1
done
docker exec "$container" mongosh --quiet --host 127.0.0.1 --username connector --password "$MONGO_CONNECTOR" \
  --authenticationDatabase activity --eval '
    const a = db.getSiblingDB("activity");
    assert.commandWorked(a.runCommand({find:"days", filter:{}}));
    assert.commandFailedWithCode(a.runCommand({find:"settings", filter:{}}), 13);
    assert.commandFailedWithCode(a.runCommand({insert:"days", documents:[{_id:"forbidden"}]}), 13);'
dotnet run --project checks/HealthSynki.Checks -c Release
checks/http.sh
