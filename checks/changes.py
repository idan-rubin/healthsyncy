import json
import os
import subprocess

BUILD = ("Directory.Build.props", ".editorconfig", ".editorconfig.ci", ".globalconfig")
SELECTION = (".github/workflows/ci.yml", "checks/changes.py")

AREAS = {
    "backend": BUILD + SELECTION + (
        "src/", "tests/", "checks/", "scripts/", "deploy/", "HealthSyncy.slnx", "global.json"),
    "ios": BUILD + SELECTION + (
        "src/HealthSyncy.Mobile/",
        "src/HealthSyncy.Sync/HealthSyncy.Sync.Contracts/",
        "src/HealthSyncy.Common/HealthSyncy.Common.Contracts/"),
    "services": (
        "src/HealthSyncy.Common/", "src/HealthSyncy.Sync/", "src/HealthSyncy.Connector/", "src/HealthSyncy.Admin/",
        "Directory.Build.props", ".editorconfig", ".globalconfig", "Dockerfile", ".dockerignore", "LICENSE",
        "deploy/", "scripts/deploy.sh", "scripts/render.py", ".github/workflows/deploy.yml"),
}
OUTSIDE = {"backend": ("src/HealthSyncy.Mobile/HealthSyncy.Mobile.EntryPoint/",)}


def changed(base):
    if not base or set(base) == {"0"}:
        return None
    diff = subprocess.run(["git", "diff", "--name-only", base, "HEAD"], capture_output=True, text=True)
    return diff.stdout.splitlines() if diff.returncode == 0 else None


def needs(area, files):
    if files is None:
        return True
    return any(
        (file.startswith(pattern) if pattern.endswith("/") else file == pattern)
        for file in files if not file.startswith(OUTSIDE.get(area, ()))
        for pattern in AREAS[area])


def github(path):
    response = subprocess.run(["gh", "api", path], capture_output=True, text=True)
    return json.loads(response.stdout) if response.returncode == 0 else []


def last_deployed():
    repository = os.environ["GITHUB_REPOSITORY"]
    for deployment in github(f"repos/{repository}/deployments?environment=production&per_page=30"):
        statuses = github(f"repos/{repository}/deployments/{deployment['id']}/statuses")
        if any(status["state"] == "success" for status in statuses):
            return deployment["sha"]
    return None


event = os.environ["GITHUB_EVENT_NAME"]
if event == "pull_request":
    proposed = changed("HEAD^1")
    selected = {area: needs(area, proposed) for area in AREAS}
elif event == "push":
    pushed = changed(os.environ.get("PUSH_BEFORE"))
    services = needs("services", changed(last_deployed()))
    selected = {"backend": services or needs("backend", pushed), "ios": needs("ios", pushed), "services": services}
else:
    selected = dict.fromkeys(AREAS, True)

with open(os.environ.get("GITHUB_OUTPUT", os.devnull), "a", encoding="utf-8") as output:
    for area, needed in selected.items():
        output.write(f"{area}={str(needed).lower()}\n")
        print(f"{area}: {'runs' if needed else 'skipped'}")
