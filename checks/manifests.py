from pathlib import Path
import json
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import plistlib

root = Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory() as d:
    render = ["python3", str(root/"scripts/render.py"), "--domain","example.org","--registry","ghcr.io/example/healthsyncy","--tag","a"*40,"--output",d]
    subprocess.run(render, check=True)
    resources = [json.loads(x) for x in (Path(d)/"resources.yaml").read_text().split("\n---\n")]
    services = [r for r in resources if r["kind"]=="Service"]
    assert {r["metadata"]["name"] for r in services}=={"mongo","sync","admin","connector"}
    assert all(r["spec"].get("type","ClusterIP")=="ClusterIP" for r in services)
    ingress = next(r for r in resources if r["kind"]=="Ingress")["spec"]
    assert all(rule["host"] != "mongo.example.org" for rule in ingress["rules"])
    assert set(ingress["tls"][0]["hosts"])=={r["host"] for r in ingress["rules"]}
    assert all(r["metadata"]["namespace"]=="healthsyncy" for r in resources)
    assert all("storageClassName" not in r["spec"] for r in resources if r["kind"] == "PersistentVolumeClaim")
    subprocess.run([*render, "--shared-node"], check=True)
    shared = [json.loads(x) for x in (Path(d)/"resources.yaml").read_text().split("\n---\n")]
    assert all(r["metadata"]["namespace"] == "healthsyncy" for r in shared)
    policies = {r["metadata"]["name"]: r["spec"] for r in shared if r["kind"] == "NetworkPolicy"}
    assert policies["default-deny"] == {"podSelector": {}, "policyTypes": ["Ingress", "Egress"]}
    ingress_peers = policies["app-traffic"]["ingress"][0]["from"]
    assert ingress_peers == [{
        "namespaceSelector": {"matchLabels": {"kubernetes.io/metadata.name": "kube-system"}},
        "podSelector": {"matchLabels": {"app.kubernetes.io/name": "traefik"}},
    }]
    assert policies["mongo-private"]["ingress"][0]["from"][0].keys() == {"podSelector"}
    assert set(policies["oauth-https"]["podSelector"]["matchExpressions"][0]["values"]) == {"admin", "connector"}
    claims = [r["spec"]["storageClassName"] for r in shared if r["kind"] == "PersistentVolumeClaim"]
    assert claims == ["local-path", "local-path"]
    quota = next(r["spec"]["hard"] for r in shared if r["kind"] == "ResourceQuota")
    assert quota["services.loadbalancers"] == quota["services.nodeports"] == "0"
    deployments = [r["spec"]["template"]["spec"] for r in shared if r["kind"] == "Deployment"]
    assert len(deployments) == 4
    for pod in deployments:
        assert pod["automountServiceAccountToken"] is False
        limits = pod["containers"][0]["resources"]
        assert all(set(limits[key]) == {"cpu", "memory"} for key in ("requests", "limits"))
for p in root.glob("**/*.csproj"):
    for ref in ET.parse(p).iter("ProjectReference"):
        assert (p.parent/ref.attrib["Include"]).is_file()
for p in ET.parse(root/"HealthSyncy.slnx").iter("Project"):
    assert (root/p.attrib["Path"]).is_file()
for p in root.glob("mobile/**/*.plist"):
    plistlib.loads(p.read_bytes())
print("Deployment manifests, project references and iOS plists passed.")
