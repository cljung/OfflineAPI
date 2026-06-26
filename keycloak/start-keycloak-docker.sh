#!/bin/bash

# start keycloak container
docker run -d \
  --name my-keycloak \
  -p 8080:8080 \
  -e KEYCLOAK_ADMIN=admin \
  -e KEYCLOAK_ADMIN_PASSWORD=OfflineAPI \
  -v $(pwd)/data:/opt/keycloak/data/import \
  quay.io/keycloak/keycloak:26.1.3 \
  start-dev --import-realm

docker ps

echo "to stop and remove keycloak container do"
echo "docker stop my-keycloak"
echo "docker rm my-keycloak"
