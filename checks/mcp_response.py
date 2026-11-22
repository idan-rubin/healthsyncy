import json
import sys
from pathlib import Path

mode, filename = sys.argv[1:]
body = Path(filename).read_text()
messages = [json.loads(v[5:]) for v in body.splitlines() if v.startswith("data:")] if not body.lstrip().startswith("{") else [json.loads(body)]
result = next(m["result"] for m in messages if m.get("id") in (2, 3))
if mode == "list":
    assert {item["name"] for item in result["tools"]} == {"get_activity_day", "get_activity_week"}
    assert all(item["annotations"]["readOnlyHint"] for item in result["tools"])
else:
    assert not result.get("isError", False)
    value = next(json.loads(item["text"]) for item in result["content"] if item["type"] == "text")
    assert value["snapshot"]["totals"]["steps"] == 123
