from pathlib import Path
import json
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import plistlib

root = Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory() as d:
    subprocess.run(["python3", str(root/"scripts/render.py"), "--domain","example.org","--registry","ghcr.io/example/healthsynki","--tag","a"*40,"--output",d],check=True)
    resources = [json.loads(x) for x in (Path(d)/"resources.yaml").read_text().split("\n---\n")]
    services = [r for r in resources if r["kind"]=="Service"]
    assert {r["metadata"]["name"] for r in services}=={"mongo","sync","admin","connector"}
    assert all(r["spec"].get("type","ClusterIP")=="ClusterIP" for r in services)
    ingress = next(r for r in resources if r["kind"]=="Ingress")["spec"]
    assert all(rule["host"] != "mongo.example.org" for rule in ingress["rules"])
    assert set(ingress["tls"][0]["hosts"])=={r["host"] for r in ingress["rules"]}
    assert all(r["metadata"]["namespace"]=="healthsynki" for r in resources)
for p in root.glob("**/*.csproj"):
    for ref in ET.parse(p).iter("ProjectReference"):
        assert (p.parent/ref.attrib["Include"]).is_file()
for p in ET.parse(root/"HealthSynki.slnx").iter("Project"):
    assert (root/p.attrib["Path"]).is_file()
for p in root.glob("mobile/**/*.plist"):
    plistlib.loads(p.read_bytes())
print("Deployment manifests, project references and iOS plists passed.")
