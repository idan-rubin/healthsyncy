import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

root = Path(__file__).resolve().parents[1]
results, required = Path(sys.argv[1]), float(sys.argv[2])
expected = {project.parent.name for project in root.glob("src/*/*/*.csproj")} - {"HealthSyncy.Mobile.EntryPoint"}

hits = {}
for report in results.rglob("*.cobertura.xml"):
    for source in ET.parse(report).getroot().iter("class"):
        path = Path(source.attrib["filename"].replace("\\", "/")).resolve()
        if not path.is_relative_to(root):
            continue
        parts = path.relative_to(root).parts
        if parts[0] != "src" or "obj" in parts:
            continue
        for line in source.iterfind("lines/line"):
            key = (parts, int(line.attrib["number"]))
            hits[key] = max(hits.get(key, 0), int(line.attrib["hits"]))
if not hits:
    sys.exit(f"No coverage reports with product code under {results}.")

projects = defaultdict(lambda: [0, 0])
for (parts, _), count in hits.items():
    tally = projects[parts[2]]
    tally[0] += count > 0
    tally[1] += 1
for project, (covered, lines) in sorted(projects.items()):
    print(f"{project:<42}{covered:>5}/{lines:<5}{covered / lines:>7.1%}")

if missing := sorted(expected - projects.keys()):
    sys.exit(f"No coverage for {', '.join(missing)}. Run the tests that load it.")
covered, lines = (sum(tally[i] for tally in projects.values()) for i in (0, 1))
print(f"Line coverage {covered}/{lines} = {covered / lines:.1%} (required {required:g}%).")
if covered / lines * 100 < required:
    sys.exit("Coverage is below the required level.")
