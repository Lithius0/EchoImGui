from pathlib import Path

# This script converts ImGui's vectors to Unity vectors.
# It just replaces the references to System.Numerics with UnityEngine.
# Probably not super robust but it works in this case. 

root_dir = Path(__file__).resolve().parent
for path in root_dir.rglob("*.cs"):
    # print(path)
    with open(path, "r+") as f:
        text = f.read()
        f.seek(0)
        f.write(text.replace("using System.Numerics", "using UnityEngine"))
        f.truncate()