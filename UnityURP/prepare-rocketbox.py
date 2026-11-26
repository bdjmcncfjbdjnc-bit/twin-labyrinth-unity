import io
import json
import shutil
import struct
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT.parent / "ReferenceCharacters" / "Rocketbox"
TARGET = ROOT / "Assets" / "ArtReview" / "Source" / "MazeCast"
OUTFITS = {
    "marcus_formal": ("Skin_shirt", "Top_shirt", "Trousers"),
    "hannah_casual": ("Skin_original", "Outfit_original"),
    "kenji_formal": ("Skin_tee", "Top_tee", "Trousers"),
}


def align(data, fill):
    return data + fill * (-len(data) % 4)


def convert(name, outfit):
    contents = (SOURCE / f"{name}.glb").read_bytes()
    json_size, json_type = struct.unpack_from("<I4s", contents, 12)
    if contents[:4] != b"glTF" or json_type != b"JSON":
        raise ValueError(f"Invalid source GLB: {name}")
    document = json.loads(contents[20:20 + json_size])
    binary = memoryview(contents)[28 + json_size:]
    keep = {"Body", "Hair", "Shoes", *outfit}

    for node in document["nodes"]:
        if "mesh" in node and node["name"] not in keep:
            for key in ("mesh", "skin", "weights"):
                node.pop(key, None)
    document["animations"] = [clip for clip in document["animations"] if clip["name"] in {"Idle", "Walk"}]
    for mesh in document["meshes"]:
        if "weights" in mesh:
            mesh["weights"] = [0] * len(mesh["weights"])
    for texture in document["textures"]:
        extensions = texture.get("extensions", {})
        webp = extensions.pop("EXT_texture_webp", None)
        if webp is not None:
            texture["source"] = webp["source"]
        if not extensions:
            texture.pop("extensions", None)
    for field in ("extensionsUsed", "extensionsRequired"):
        if field in document:
            document[field] = [extension for extension in document[field] if extension != "EXT_texture_webp"]

    images = {image["bufferView"]: image for image in document["images"]}
    result = bytearray()
    for index, view in enumerate(document["bufferViews"]):
        start = view.get("byteOffset", 0)
        payload = binary[start:start + view["byteLength"]]
        image = images.get(index)
        if image is not None and image["mimeType"] == "image/webp":
            with Image.open(io.BytesIO(payload)) as texture:
                texture.thumbnail((1024, 1024), Image.Resampling.LANCZOS)
                buffer = io.BytesIO()
                linear = any(value in image.get("name", "").lower() for value in ("rough", "normal", "metal"))
                if linear or "A" in texture.getbands():
                    texture.save(buffer, "PNG", optimize=True)
                    image["mimeType"] = "image/png"
                else:
                    texture.convert("RGB").save(buffer, "JPEG", quality=90)
                    image["mimeType"] = "image/jpeg"
                payload = buffer.getvalue()
        view["byteOffset"] = len(result)
        view["byteLength"] = len(payload)
        result.extend(payload)
        result.extend(b"\0" * (-len(result) % 4))
    document["buffers"][0]["byteLength"] = len(result)
    encoded = align(json.dumps(document, separators=(",", ":")).encode("utf-8"), b" ")
    with (TARGET / f"{name}.glb").open("wb") as file:
        file.write(struct.pack("<4sII", b"glTF", 2, 28 + len(encoded) + len(result)))
        file.write(struct.pack("<I4s", len(encoded), b"JSON"))
        file.write(encoded)
        file.write(struct.pack("<I4s", len(result), b"BIN\0"))
        file.write(result)
    print(f"{name}: {len(document['skins'][0]['joints'])} joints, {len(document['animations'])} clips")


TARGET.mkdir(parents=True, exist_ok=True)
for model, outfit in OUTFITS.items():
    convert(model, outfit)
shutil.copyfile(SOURCE / "THIRD_PARTY_NOTICES.md", TARGET / "Rocketbox-Notices.txt")
