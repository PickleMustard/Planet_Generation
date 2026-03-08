# GDUnit4 Version Research for Godot 4.4+ Compatibility

**Research Date:** March 7, 2026  
**Sources:**
- GitHub Repository: https://github.com/godot-gdunit-labs/gdUnit4
- Releases Page: https://github.com/godot-gdunit-labs/gdUnit4/releases
- Installation Docs: https://godot-gdunit-labs.github.io/gdUnit4/latest/first_steps/install/

---

## Latest Stable Version

**Current Latest Stable Release:** v6.1.1  
**Release Date:** January 30, 2026  
**Repository:** https://github.com/godot-gdunit-labs/gdUnit4

---

## Godot Compatibility Matrix

| GDUnit4 Version | Compatible Godot Versions |
|----------------|---------------------------|
| master (upcoming v6.2) | v4.5, v4.5.1, v4.6 |
| **v6.1.x** | v4.5, v4.5.1, v4.6 |
| v6.0.x+ | v4.5, v4.5.1 |
| v5.x+ | v4.3, v4.4, v4.4.1 |
| v4.4.0+ | v4.2.0, v4.3, v4.4.dev2 |

### Important Notes:
- The latest version of GdUnit4 (master branch) is working with Godot **v4.4.stable.mono.official [4c311cbee]**
- **Breaking Change:** GdUnit4 v6.0.x+ is based on Godot 4.5.0 and is no longer backward compatible with older versions
- Godot 4.5 introduced API changes that required a complete rebuild of the framework

---

## Version Recommendations by Godot Version

### For Godot 4.6.x
✅ **Recommended:** GDUnit4 v6.1.x (latest: v6.1.1)  
- Full compatibility with Godot 4.6.x
- Includes all latest features and bug fixes
- Supports v4.5, v4.5.1, and v4.6

### For Godot 4.4.x (4.4 or 4.4.1)
✅ **Recommended:** GDUnit4 v5.x (latest: v5.1.1)  
- Designed for Godot 4.3, 4.4, and 4.4.1
- Will NOT work with v6.0.x+ which requires Godot 4.5+

### For Godot 4.5.x
✅ **Recommended:** GDUnit4 v6.1.x (latest: v6.1.1)  
- Native support for Godot 4.5 and 4.5.1
- Can also use v6.0.x if needed

---

## Installation Methods

### Method 1: Asset Library (Recommended for Most Users)

**Steps:**
1. Open Godot Editor
2. Click **AssetLib** in the top menu bar
3. Search for "GdUnit4"
4. Select the GdUnit4 plugin from results
5. Click **Download** button
6. Accept files and press **Install**
7. Activate the plugin (see activation steps below)
8. **Restart Godot Editor** (recommended to avoid cache issues)

**Advantages:**
- Easiest installation method
- Automatic updates through AssetLib
- No manual file management needed

### Method 2: GitHub Release (For Specific Versions)

**Steps:**
1. Go to: https://github.com/godot-gdunit-labs/gdUnit4/releases
2. Download the desired version (e.g., v6.1.1)
3. Disable current GdUnit4 plugin if installed
4. Delete existing `addons/gdunit4` folder
5. Extract downloaded package to `addons` folder
6. Activate the plugin

**Advantages:**
- Install specific versions
- Access to pre-release versions
- Manual control over updates

### Method 3: Latest Master Branch (Bleeding Edge)

**Steps:**
1. Download: https://github.com/godot-gdunit-labs/gdUnit4/archive/refs/heads/master.zip
2. Disable current GdUnit4 plugin
3. Delete existing `addons/gdunit4` folder
4. Extract to `addons` folder
5. Activate plugin

**Advantages:**
- Latest bug fixes before official release
- Access to newest features

**Risks:**
- May contain unreleased bugs
- Less stable than official releases

---

## Plugin Activation

After installation (any method):

1. Open **Project → Project Settings**
2. Click **Plugins** tab
3. Find **GdUnit4** in the list
4. Check the checkbox to **activate**
5. **Save** project settings
6. **Restart Godot Editor** (highly recommended)

The GdUnit4 inspector will appear in the top-left corner of the editor after activation.

---

## Version History Highlights

### v6.1.1 (Jan 30, 2026) - Latest Stable
- Hot fix release
- Fixed compile errors and warnings
- Fixed error monitor to respect `push_error` report settings

### v6.1.0 (Jan 27, 2026)
- Added "Run until failure" context menu
- Added variadic argument support to assert_signal
- Improved orphan detection and reporting
- Fixed scene runner errors
- Added Godot 4.6.x compatibility
- Refactored context menus to use EditorContextMenuPlugin
- Replaced manual error log parsing with Godot Logger

### v6.0.0 (Oct 5, 2025) - Breaking Change
- **Major version bump** - requires Godot 4.5+
- Added session hooks API
- Added Unicode character support
- Added variadic argument support
- No longer backward compatible with Godot < 4.5

### v5.1.1 (Sep 20, 2025)
- Fixed C# test internal errors
- Fixed SceneRunner SimulateKey unicode issues
- Fixed Linux C# test discovery issues

### v5.1.0 (Aug 17, 2025)
- Redesigned inspector test statistics UI
- Introduced test session hooks (undocumented until v6.0.0)
- Added argument matchers on assert_error

---

## For Your Project (Planet Generation)

Given your project uses **Godot 4.4+** (as specified in AGENTS.md):

### If using Godot 4.4 or 4.4.1:
```
Recommended: GDUnit4 v5.1.1
Download: https://github.com/godot-gdunit-labs/gdUnit4/releases/tag/v5.1.1
```

### If using Godot 4.5, 4.5.1, or 4.6.x:
```
Recommended: GDUnit4 v6.1.1
Download: https://github.com/godot-gdunit-labs/gdUnit4/releases/tag/v6.1.1
```

### Installation Command (if using GitHub release):
```bash
# For v6.1.1 (Godot 4.5+/4.6+)
wget https://github.com/godot-gdunit-labs/gdUnit4/releases/download/v6.1.1/gdUnit4.zip
unzip gdUnit4.zip -d addons/

# For v5.1.1 (Godot 4.4/4.4.1)
wget https://github.com/godot-gdunit-labs/gdUnit4/releases/download/v5.1.1/gdUnit4.zip
unzip gdUnit4.zip -d addons/
```

---

## Additional Resources

- **Official Documentation:** https://godot-gdunit-labs.github.io/gdUnit4/latest/
- **GitHub Repository:** https://github.com/godot-gdunit-labs/gdUnit4
- **Issue Tracker:** https://github.com/godot-gdunit-labs/gdUnit4/issues
- **Discord Server:** https://discord.gg/rdq36JwuaJ
- **GitHub Action for CI:** https://github.com/marketplace/actions/gdunit4-test-runner-action

---

## C# Project Setup

For C# projects (like your Planet Generation project), additional setup is required:

1. Install the gdUnit4.api NuGet package:
   ```xml
   <PackageReference Include="gdUnit4.api" Version="5.1.0-rc1"/>
   ```

2. Configure your IDE for VSTest integration:
   - Visual Studio
   - Visual Studio Code
   - JetBrains Rider

See: https://godot-gdunit-labs.github.io/gdUnit4/latest/csharp_project_setup/csharp-setup/

---

## Known Issues & Solutions

- **ScriptResource errors after plugin installation:** Restart Godot Editor (project cache issue)
- **C# test discovery issues on Linux:** Fixed in v5.1.1+
- **Duplicate key exception in ScriptTypeBiMap:** Fixed in v6.0.3+

---

## Summary

**For Godot 4.6.x:** Use GDUnit4 v6.1.1 (latest stable)  
**For Godot 4.4.x:** Use GDUnit4 v5.1.1 (last compatible version)  
**For Godot 4.5.x:** Use GDUnit4 v6.1.1 (native support)

**Installation:** AssetLib (easiest) or GitHub releases (specific versions)

**Important:** Always restart Godot Editor after installing/updating the plugin to avoid cache issues.
