---
description: Automate quarterly BeNomad map version updates for the InTruck application. Run with a version argument like `/intruck-maps-update 2026.1`. Handles license file rename, .csproj reference, RouteProcessorTests, and config.json updates.
user_invocable: true
argument: version number (e.g. 2026.1)
---

# InTruck Maps Quarterly Update

You are performing a quarterly BeNomad map version update. The user has provided a target version as the argument.

## Step 0: Parse the version

- Extract the **year** and **quarter** from the argument (e.g. `2026.1` → year=2026, quarter=1).
- Derive the **3-version rolling window**: the new file covers the previous two versions plus the new one. For example, if the new version is `2026.1`, the window is `2025.3-2025.4-2026.1`. When rolling back from quarter 1, wrap to the previous year's quarter 4; from quarter 2, use the same year's quarter 1 and previous year's quarter 4; etc.
- Determine the **HTTP port** from the quarter:

| Quarter | Port |
|---------|------|
| .1 (Q1) | 4040 |
| .2 (Q2) | 5050 |
| .3 (Q3) | 6060 |
| .4 (Q4) | 8080 |

- Determine the **previous version** (one quarter back) and its port — this is the version being replaced as the "current" default.
- Determine the **oldest entry to remove** from `beMapServers` in RouteProcessorTests.cs: find the entry whose port conflicts with the new version's port (same port number) and remove it.

## Step 1: Prompt for license binary

Tell the user:
> Before proceeding, please place the new `benomad.lic` file in:
> `jtrack-domestic/JJRichards.JTrack.Domestic.InTruck/BeNomad_License/`
>
> Let me know when it's ready, or say "skip" to handle it later.

Wait for confirmation before continuing (or skip if told to).

## Step 2: Rename license text file

- Find the existing `.txt` file in `jtrack-domestic/JJRichards.JTrack.Domestic.InTruck/BeNomad_License/` matching pattern `JJRichards_OEM_AUS-NZL_NT_*.txt`.
- `git mv` it to the new 3-version window filename. For example:
  ```
  git mv JJRichards_OEM_AUS-NZL_NT_2025.3-2025.4-2026.1.txt → (would be the result if updating to 2026.2, for instance)
  ```

## Step 3: Update .csproj reference

- **File:** `jtrack-domestic/JJRichards.JTrack.Domestic.InTruck/JJRichards.JTrack.Domestic.InTruck.csproj`
- Find the `<Resource Include="BeNomad_License\JJRichards_OEM_AUS-NZL_NT_...txt" />` line and update the filename to match the renamed file from Step 2.

## Step 4: Update RouteProcessorTests.cs

- **File:** `jtd-routerbuilder-service/JJRichards.Services.Builder.Api.Tests/Services/BeMap/RouteProcessorTests.cs`
- In the `beMapServers` dictionary:
  1. **Remove** the entry whose port matches the new version's port (the conflicting old entry).
  2. **Add** a new entry: `{ "<new_version>", "http://devbenomad:<new_port>" }`
- Update `targetMapVersion` in the second test to the new version string.

## Step 5: Update config.json

- **File:** `jtd-routerbuilder-service/JJRichards.Services.Builder/config.json`
- `tileServer`: change the port to the new version's port (keep `/bgis/wms` path).
- `defaultMapVersion`: set to the new version string.
- `beMapServers`: set to `"<new_version>,http://devbenomad:<new_port>"`.

## Step 6: Show diff and reminders

Run `git diff` and `git status` to show all changes for review.

Then remind the user:
1. **License binary:** If not already done, place the new `benomad.lic` in `BeNomad_License/` before committing.
2. **DevBenomad:** The routebuilder-service build/tests will fail until the new BeNomad map service is running on `devbenomad` at port `<new_port>`.
