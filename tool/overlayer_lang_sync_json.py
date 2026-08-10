"""
This script synchronizes localization JSON files with the reference 'en-US' baseline.
It filters target files using the '0KTL' marker key, purges redundant keys,
copies missing keys from English, and saves all outputs sorted alphabetically.
Logs are saved in the ./.ktl directory.
"""

import os
import json
import glob
from datetime import datetime

BASE_DIR = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OVERLAYER_DIR = os.path.join(BASE_DIR, "Overlayer")
LANG_DIR = os.path.join(OVERLAYER_DIR, "Resource", "Export", "Lang")
LOG_DIR = os.path.join(BASE_DIR, ".ktl")

def process_language_files():
    os.makedirs(LOG_DIR, exist_ok=True)
    
    log_filename = f"ktl_sync_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
    log_file_path = os.path.join(LOG_DIR, log_filename)
    
    log_entries = []

    def log(message):
        log_entries.append(message)
        print(message)

    log(f"Starting localization JSON synchronization at: {datetime.now()}")
    log(f"Target directory: {LANG_DIR}\n")

    if not os.path.exists(LANG_DIR):
        log(f"Error: Target directory '{LANG_DIR}' does not exist.")
        return

    json_files = glob.glob(os.path.join(LANG_DIR, "*.json"))
    valid_files = {}

    for filepath in json_files:
        try:
            with open(filepath, 'r', encoding='utf-8') as f:
                data = json.load(f)
                
            for lang_code, translations in data.items():
                if isinstance(translations, dict) and translations.get("0KTL") == "DO_NOT_TRANSLATE_THIS_KEY!":
                    valid_files[filepath] = (lang_code, translations)
                    break
        except Exception as e:
            log(f"Failed to read or parse file '{filepath}': {e}")

    english_filepath = None
    english_data = None

    for filepath, (lang_code, translations) in valid_files.items():
        if lang_code == "en-US":
            english_filepath = filepath
            english_data = translations
            break

    if not english_data:
        log("Error: 'en-US' reference language key not found in any valid JSON files.")
        return

    log(f"Reference language template 'en-US' loaded from: {os.path.basename(english_filepath)}\n")

    english_keys = set(english_data.keys())

    for filepath, (lang_code, translations) in valid_files.items():
        filename = os.path.basename(filepath)
        log(f"--- Processing File: {filename} ({lang_code}) ---")

        if lang_code == "en-US":
            sorted_english = {k: english_data[k] for k in sorted(english_data.keys())}
            with open(filepath, 'w', encoding='utf-8') as f:
                json.dump({"en-US": sorted_english}, f, ensure_ascii=False, indent=2)
            log("Result: Re-ordered en-US keys alphabetically.\n")
            continue

        current_keys = set(translations.keys())

        keys_to_remove = current_keys - english_keys
        keys_to_insert = english_keys - current_keys

        for key in keys_to_remove:
            del translations[key]
            log(f" [Removed Key]  '{key}'")

        for key in keys_to_insert:
            translations[key] = english_data[key]
            log(f" [Inserted Key] '{key}' (copied value from en-US)")

        sorted_translations = {k: translations[k] for k in sorted(translations.keys())}

        output_payload = {lang_code: sorted_translations}
        with open(filepath, 'w', encoding='utf-8') as f:
            json.dump(output_payload, f, ensure_ascii=False, indent=2)

        log(f"Summary: Removed {len(keys_to_remove)} keys, Inserted {len(keys_to_insert)} keys. File saved.\n")

    with open(log_file_path, 'w', encoding='utf-8') as f:
        f.write("\n".join(log_entries))

    print(f"Synchronization finished. Audit log generated at: {log_file_path}")

if __name__ == "__main__":
    process_language_files()