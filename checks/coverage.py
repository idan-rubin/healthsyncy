import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

results, required = Path(sys.argv[1]), float(sys.argv[2])
hits = {}
for report in results.rglob("*.cobertura.xml"):
    for source in ET.parse(report).getroot().iter("class"):
        name = source.attrib["filename"].replace("\\", "/")
        if "/obj/" in name or "/src/" not in name:
            continue
        for line in source.iterfind("lines/line"):
            key = (name, int(line.attrib["number"]))
            hits[key] = max(hits.get(key, 0), int(line.attrib["hits"]))
if not hits:
    sys.exit(f"No coverage reports with product code under {results}.")

projects = defaultdict(lambda: [0, 0])
for (name, _), count in hits.items():
    tally = projects[name.split("/src/", 1)[1].split("/")[1]]
    tally[0] += count > 0
    tally[1] += 1
for project, (covered, lines) in sorted(projects.items()):
    print(f"{project:<42}{covered:>5}/{lines:<5}{covered / lines:>7.1%}")

covered, lines = (sum(tally[i] for tally in projects.values()) for i in (0, 1))
print(f"Line coverage {covered}/{lines} = {covered / lines:.1%} (required {required:g}%).")
if covered / lines * 100 < required:
    sys.exit("Coverage is below the required level.")
