## ⚠️ Disclaimer & Authorship

**I do not write code personally.** This tool was created for my own needs using Artificial Intelligence assistance.

While this project is provided as **Open Source** for the community to use and learn from, the specific implementation, concept, and the tool itself remain my intellectual property.

Please note: Since this is an AI-assisted project, I may not be able to fix complex code issues manually. Use this software at your own risk.

# GlamBatcher

GlamBatcher is a lightweight, standalone Windows tool designed for Final Fantasy XIV players who use the **Glamourer** plugin. It allows you to bulk-edit customization attributes across multiple saved design files (`.json`) simultaneously.

Instead of editing dozens of designs manually to update a hairstyle or face paint, GlamBatcher does it in seconds.

## 🚀 Features

* **Batch Editing:** Update **Hairstyle**, **Face ID**, **Tail/TailShape**, and **Face Paint** for multiple designs at once.
* **Smart Grouping:** Automatically groups designs by Clan/Subrace (e.g., `(Viera) Rava`) to prevent invalid customization combinations.
* **Auto-Read Values:** Selecting a design automatically pre-fills the input fields with the current values.
* **Smart Save:** Safely updates the JSON structure (handles `Value`/`Apply` logic) without breaking the file.
* **Tail Support:** Automatically detects if a race has a tail and handles both legacy (`Tail`) and modern (`TailShape`) data fields.
* **Auto-Updater:** Notifies you when a new version is available on GitHub.

## 🛠 Usage

1.  Make sure FFXIV is closed (to prevent overwrite conflicts).
2.  Run `GlamBatcher.exe`.
3.  Click **Load Designs** (automatically finds your XIVLauncher/Glamourer path).
4.  Select the designs you want to modify.
5.  Change the values (checkboxes enable automatically).
6.  Click **Apply Changes**.

## 📦 Requirements

* Windows 10/11 (64-bit)
* Installed FFXIV with XIVLauncher & Glamourer Plugin
