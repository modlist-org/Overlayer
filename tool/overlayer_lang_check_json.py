"""
This script verifies localization keys between C# source code files (.cs) in Overlayer
and the 'en-US' reference JSON baseline.
It finds unused JSON keys and missing keys referenced in code, outputting a summary to terminal
and full details to a timestamped log inside ./.ktl directory.
"""

import os
import json
import glob
import re
from datetime import datetime

BASE_DIR = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OVERLAYER_DIR = os.path.normpath(os.path.join(BASE_DIR, "Overlayer"))
LANG_DIR = os.path.normpath(os.path.join(OVERLAYER_DIR, "Resource", "Export", "Lang"))
LOG_DIR = os.path.normpath(os.path.join(BASE_DIR, ".ktl"))
IGNORED_KEYS = {"0KTL", "0TRANSLATORS"}

literal_pattern = re.compile(r'(?<![\w])"([A-Za-z0-9_][A-Za-z0-9_ /.\-]*)"')
code_editor_label_pattern = re.compile(r'\bCodeEditor\(\s*[^,]+,\s*"([^"]+)"')
vector2_label_pattern = re.compile(r'\bVector2Sliders\(\s*[^,]+,\s*"([^"]+)"')
component_label_pattern = re.compile(r'\bComponentCard\(\s*"([^"]+)"')

def inspector_key(label):
    return "INSPECTOR_" + label.replace(" / ", "_").replace(" ", "_").replace(".", "").upper()

def component_key(label):
    return "COMPONANT_" + label.replace(" ", "_").upper()

def check_language_keys():
    os.makedirs(LOG_DIR, exist_ok=True)
    log_filename = f"ktl_check_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
    log_file_path = os.path.join(LOG_DIR, log_filename)

    log_entries = []
    def log(message):
        log_entries.append(message)

    log(f"Starting localization key check at: {datetime.now()}")
    log(f"Target Overlayer directory: {OVERLAYER_DIR}")
    log(f"Target Language directory: {LANG_DIR}\n")

    en_keys = set()
    json_files = glob.glob(os.path.join(LANG_DIR, "*.json"))
    
    for filepath in json_files:
        try:
            with open(filepath, 'r', encoding='utf-8') as f:
                data = json.load(f)
                if "en-US" in data and isinstance(data["en-US"], dict):
                    en_keys = set(data["en-US"].keys())
                    log(f"Reference 'en-US' loaded from: {os.path.basename(filepath)} ({len(en_keys)} keys found)")
                    break
        except Exception as e:
            log(f"Failed to read file '{filepath}': {e}")

    if not en_keys:
        print("Error: Could not load 'en-US' key baseline from JSON files.")
        return

    valid_en_keys = en_keys - IGNORED_KEYS

    cs_files = glob.glob(os.path.join(OVERLAYER_DIR, "**", "*.cs"), recursive=True)
    
    code_referenced_keys = set()
    key_locations = {}  # key -> list of 'filename:line_num'

    def add_reference(key, location):
        if key not in valid_en_keys:
            return
        code_referenced_keys.add(key)
        locations = key_locations.setdefault(key, [])
        if location not in locations:
            locations.append(location)

    for cs_path in cs_files:
        rel_path = os.path.relpath(cs_path, OVERLAYER_DIR)
        try:
            with open(cs_path, 'r', encoding='utf-8', errors='ignore') as f:
                for line_idx, line in enumerate(f, start=1):
                    stripped = line.strip()
                    if stripped.startswith("//") or stripped.startswith("/*"):
                        continue
                    
                    matches = literal_pattern.findall(line)
                    generated_keys = []
                    for match in matches:
                        generated_keys.append(inspector_key(match))
                        add_reference(match, f"{rel_path}:{line_idx}")

                    if "CodeEditor(" in line:
                        generated_keys.extend(
                            inspector_key(f"{label} / tag expression")
                            for label in code_editor_label_pattern.findall(line)
                        )

                    if "Vector2Sliders(" in line:
                        for label in vector2_label_pattern.findall(line):
                            generated_keys.extend((inspector_key(f"{label} X"), inspector_key(f"{label} Y")))

                    if "ComponentCard(" in line:
                        generated_keys.extend(component_key(label) for label in component_label_pattern.findall(line))

                    for key in generated_keys:
                        add_reference(key, f"{rel_path}:{line_idx}")
                            
        except Exception as e:
            log(f"Failed to process CS file '{cs_path}': {e}")

    unused_in_code = sorted(list(valid_en_keys - code_referenced_keys))
    
    log("\n==================================================")
    log("DETAILED AUDIT REPORT")
    log("==================================================\n")

    log(f"Unused Keys (In JSON baseline, but NOT referenced in CS files) [{len(unused_in_code)}]:")
    for key in unused_in_code:
        log(f'  "{key}"')

    log(f"\nActive Keys (Referenced in CS files) [{len(code_referenced_keys)}]:")
    for key in sorted(list(code_referenced_keys)):
        locs = ", ".join(key_locations[key][:3])
        more = f" (+{len(key_locations[key])-3} more)" if len(key_locations[key]) > 3 else ""
        log(f'  "{key}" at {locs}{more}')

    with open(log_file_path, 'w', encoding='utf-8') as f:
        f.write("\n".join(log_entries))

    print("=== Localization Check Finished ===")
    print(f"- Total Reference Keys (JSON) : {len(valid_en_keys)}")
    print(f"- Keys Used in CS Files       : {len(code_referenced_keys)}")
    print(f"- Unused Keys (JSON only)     : {len(unused_in_code)}")
    print(f"\nDetailed log generated at: {log_file_path}")

if __name__ == "__main__":
    check_language_keys()
