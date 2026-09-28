"""
Скачивает превью новой мебели (Pleb Masters Forge) в src_cef/src/views/house/furniture/props/<модель>.jpg.
Запуск из корня репозитория:  python tools/furniture_previews/download.py
Потом пересобрать интерфейс (src_cef: npm run build) — картинки подхватятся автоматически.
Адреса собраны со страниц https://forge.plebmasters.de/objects/<модель> (og:image).
"""
import json
import os
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
TARGET = os.path.join(ROOT, "src_cef", "src", "views", "house", "furniture", "props")
SOURCE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "previews.json")


def main():
    previews = json.load(open(SOURCE, encoding="utf-8"))
    os.makedirs(TARGET, exist_ok=True)
    ok = failed = skipped = 0
    for model, url in previews.items():
        path = os.path.join(TARGET, model + ".jpg")
        if os.path.exists(path) and "--force" not in sys.argv:
            skipped += 1
            continue
        try:
            request = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
            data = urllib.request.urlopen(request, timeout=30).read()
            with open(path, "wb") as file:
                file.write(data)
            ok += 1
        except Exception as error:
            failed += 1
            print(f"{model}: {error}")
    print(f"Скачано {ok}, уже было {skipped}, ошибок {failed}. Папка: {TARGET}")


if __name__ == "__main__":
    main()
