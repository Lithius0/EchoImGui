from pathlib import Path

# This script converts ImGui's vectors to Unity vectors. 

root_dir = Path(__file__).resolve().parent
for path in root_dir.rglob("*.gen.cs"):
    # print(path)
    with open(path, "r+") as f:
        f.write(f.read().replace("using System.Numerics", "using UnityEngine"))