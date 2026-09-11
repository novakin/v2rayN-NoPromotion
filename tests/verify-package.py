"""Validate archive contents, not only size. Standard library only."""
from pathlib import Path
import hashlib
import sys
import zipfile


def digest(stream):
    result = hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
        result.update(chunk)
    return result.hexdigest()


def main():
    original, result, built = map(Path, sys.argv[1:])
    with zipfile.ZipFile(original) as before, zipfile.ZipFile(result) as after:
        if len(after.namelist()) != len(set(after.namelist())):
            raise ValueError("Duplicate ZIP paths")
        if set(before.namelist()) != set(after.namelist()):
            raise ValueError("ZIP layout changed: added or missing entries")
        targets = [name for name in before.namelist()
                   if name.replace("\\", "/") == "v2rayN-windows-64/v2rayN.exe"]
        if len(targets) != 1:
            raise ValueError("Expected exactly one upstream application entry")
        target = targets[0]
        with built.open("rb") as data:
            built_hash = digest(data)
        for entry in before.infolist():
            if entry.is_dir():
                continue
            with before.open(entry.filename) as data:
                previous_hash = digest(data)
            with after.open(entry.filename) as data:
                final_hash = digest(data)
            if entry.filename == target:
                if final_hash != built_hash or final_hash == previous_hash:
                    raise ValueError("Final application is not the custom build")
            elif previous_hash != final_hash:
                raise ValueError("Unexpected changed file: " + entry.filename)
    if result.stat().st_size > original.stat().st_size * 1.1:
        raise ValueError("Unexpected package growth greater than 10%")
    print(f"PASS: exact original file set; only v2rayN.exe changed. "
          f"Official={original.stat().st_size}; custom={result.stat().st_size} bytes.")


if __name__ == "__main__":
    main()
