"""
Считает кастомную одежду в dlcpacks сервера — для CustomClothesCount в Chars/ClothesOffsets.cs.
Запуск: python tools/clothes_offsets/count_custom.py <папка dlcpacks>
Читает clothespack*/dlc.rpf (RPF7 без шифрования, OPEN), считает модели .ydd в коллекциях
mp_m_clothespack / mp_f_clothespack по слотам. Число на слот = максимальный номер модели + 1.
"""
import glob
import os
import re
import struct
import sys
from collections import defaultdict

SLOTS = {
    "berd": "Masks", "uppr": "Torsos", "lowr": "Legs", "hand": "Bugs", "feet": "Shoes", "teef": "Accessories",
    "accs": "Undershirts", "task": "BodyArmors", "decl": "Decals", "jbib": "Tops", "hair": "Hair",
    "p_head": "Hat", "p_eyes": "Glasses", "p_ears": "Ears", "p_lwrist": "Watches", "p_rwrist": "Bracelets",
}


def walk(f, base, prefix=""):
    f.seek(base)
    header = f.read(16)
    if header[:4] != b"7FPR":
        return []
    count, names_length, encryption = struct.unpack("<III", header[4:16])
    if encryption not in (0, 0x4E45504F):
        print(f"Зашифрован: {prefix}", file=sys.stderr)
        return []
    raw = f.read(16 * count)
    names = f.read(names_length)

    def name_at(offset):
        return names[offset:names.index(b"\0", offset)].decode("latin1")

    entries = []
    for i in range(count):
        entry = raw[i * 16:(i + 1) * 16]
        kind = struct.unpack("<I", entry[4:8])[0]
        if kind == 0x7FFFFF00:
            name, _, first, total = struct.unpack("<IIII", entry)
            entries.append(("dir", name_at(name), first, total))
        elif kind & 0x80000000 == 0:
            packed = struct.unpack("<Q", entry[:8])[0]
            entries.append(("file", name_at(packed & 0xFFFF), (packed >> 40) & 0xFFFFFF, 0))
        else:
            entries.append(("res", name_at(struct.unpack("<H", entry[:2])[0]), 0, 0))

    result = []

    def visit(index, path):
        kind, name, a, b = entries[index]
        if kind == "dir":
            for child in range(a, a + b):
                visit(child, path + name + "/" if index else path)
            return
        result.append(prefix + path + name)
        if kind == "file" and name.lower().endswith(".rpf"):
            result.extend(walk(f, base + a * 512, prefix + path + name + "::"))

    visit(0, "")
    return result


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else "."
    found = defaultdict(set)
    for path in glob.glob(os.path.join(root, "clothespack*", "dlc.rpf")):
        with open(path, "rb") as f:
            for name in walk(f, 0):
                leaf = name.split("::")[-1]
                m = re.match(r"(mp_[mf])_freemode_01_(p_)?mp_[mf]_clothespack/(p_)?([a-z]+)_(\d{3})(_[a-z])?(_\w+)?\.ydd$", leaf, re.I)
                if not m:
                    continue
                ped, prop, _, slot, index = m.group(1), m.group(2), m.group(3), m.group(4).lower(), int(m.group(5))
                key = ("p_" if prop else "") + slot
                if key in SLOTS:
                    found[(ped, SLOTS[key])].add(index)
    for ped in ("mp_m", "mp_f"):
        print("male:" if ped == "mp_m" else "female:")
        for slot in SLOTS.values():
            indexes = found.get((ped, slot), set())
            print(f'    {{ "{slot}", {max(indexes) + 1 if indexes else 0} }},')


if __name__ == "__main__":
    main()
