#!/usr/bin/env python3
"""Render the namespace-scoped K3s resources into a Kustomize directory."""
import argparse
import json
import re
from pathlib import Path

roles = ("sync", "connector", "admin")
ports = {"sync": 8080, "connector": 8080, "admin": 8080, "mongo": 27017}
namespace = "healthsynki"
root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument("--domain", required=True)
parser.add_argument("--registry", required=True)
parser.add_argument("--tag", required=True)
parser.add_argument("--output", type=Path, required=True)
args = parser.parse_args()
if not re.fullmatch(r"(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z][a-z0-9-]*", args.domain):
    parser.error("Use a DNS domain without a scheme or path.")
if not re.fullmatch(r"[a-z0-9./_-]+", args.registry) or not re.fullmatch(r"[a-f0-9]{40}", args.tag):
    parser.error("Provide a registry prefix and a full commit SHA.")
items = []

def add(kind, name, api="v1", **fields):
    items.append(dict(apiVersion=api, kind=kind, metadata=dict(name=name, namespace=namespace), **fields))

def claim(name, size):
    add("PersistentVolumeClaim", name, spec=dict(accessModes=["ReadWriteOnce"], resources={"requests": {"storage": size}}))

def secret_env(key, secret, field):
    return {"name": key, "valueFrom": {"secretKeyRef": {"name": secret, "key": field}}}

claim("mongo-data", "8Gi")
claim("admin-keys", "64Mi")
add("ConfigMap", "mongo-init", data={"init.js": (root/"deploy/k3s/mongo-init.js").read_text()})
for role in (*roles, "mongo"):
    labels = {"app.kubernetes.io/name": role}
    add("Service", role, spec=dict(selector=labels, ports=[dict(name="service", port=ports[role], targetPort=ports[role])]))
    container = {"name":role, "image":f"{args.registry}/{role}:{args.tag}", "ports":[{"containerPort":ports[role]}]}
    pod = {"automountServiceAccountToken":False, "containers":[container]}
    if role == "mongo":
        container.update(image="mongo:8.0", env=[
            secret_env(k, "mongo-init-secrets", k) for k in ("MONGO_INITDB_ROOT_USERNAME",
            "MONGO_INITDB_ROOT_PASSWORD","MONGO_SYNC","MONGO_CONNECTOR","MONGO_ADMIN")],
            volumeMounts=[dict(name="data",mountPath="/data/db"),
            dict(name="init",mountPath="/docker-entrypoint-initdb.d",readOnly=True)],
            readinessProbe=dict(tcpSocket={"port":27017},periodSeconds=5))
        pod["volumes"]=[dict(name="data",persistentVolumeClaim={"claimName":"mongo-data"}),
                        dict(name="init",configMap={"name":"mongo-init"})]
    else:
        container.update(envFrom=[{"secretRef":{"name":role+"-config"}}],
            readinessProbe=dict(httpGet={"path":"/readyz","port":8080}, periodSeconds=10),
            livenessProbe=dict(httpGet={"path":"/healthz","port":8080}, periodSeconds=20),
            securityContext={"allowPrivilegeEscalation":False,"capabilities":{"drop":["ALL"]}})
        pod["securityContext"]={"runAsNonRoot":True,"runAsUser":1654,"fsGroup":1654,"seccompProfile":{"type":"RuntimeDefault"}}
        if role == "admin":
            container["volumeMounts"]=[dict(name="keys",mountPath="/var/lib/healthsynki/keys")]
            pod["volumes"]=[dict(name="keys",persistentVolumeClaim={"claimName":"admin-keys"})]
    add("Deployment",role,"apps/v1",spec=dict(replicas=1, strategy={"type":"Recreate"} if role in ("mongo","admin")
        else {"type":"RollingUpdate"},selector={"matchLabels":labels},template={"metadata":{"labels":labels},"spec":pod}))

add("NetworkPolicy","mongo-private","networking.k8s.io/v1",spec={
    "podSelector":{"matchLabels":{"app.kubernetes.io/name":"mongo"}},"policyTypes":["Ingress"],
    "ingress":[{"from":[{"podSelector":{"matchExpressions":[{"key":"app.kubernetes.io/name","operator":"In","values":list(roles)}]}}],
                "ports":[{"protocol":"TCP","port":27017}]}]})
hosts = {r:f"{r}.{args.domain}" for r in roles}
routes = {"sync":[("/api/v1/days", "Prefix")], "connector":[("/api/v1/activity","Prefix"),("/mcp","Exact"),("/.well-known/oauth-protected-resource/mcp","Exact")],
          "admin":[("/","Prefix")]}
add("Ingress","healthsynki","networking.k8s.io/v1",spec={"ingressClassName":"traefik",
    "tls":[{"hosts":list(hosts.values()),"secretName":"healthsynki-tls"}],"rules":[
    {"host":hosts[r],"http":{"paths":[{"path":path,"pathType":kind,"backend":{"service":{"name":r,"port":{"number":8080}}}}
        for path,kind in routes[r]]}} for r in roles]})
items[-1]["metadata"]["annotations"]={"traefik.ingress.kubernetes.io/router.entrypoints":"websecure",
    "traefik.ingress.kubernetes.io/router.tls":"true"}
args.output.mkdir(parents=True,exist_ok=True)
(args.output/"resources.yaml").write_text("\n---\n".join(json.dumps(x,indent=2) for x in items)+"\n")
(args.output/"kustomization.yaml").write_text("apiVersion: kustomize.config.k8s.io/v1beta1\nkind: Kustomization\nresources:\n- resources.yaml\n")
