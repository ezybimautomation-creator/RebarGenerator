# Rebar Generator: Commercial Feature Roadmap

> **Last Updated:** 2026-03-07 | **Build Status:** ✅ Passing (0 errors, 0 warnings)
> **Target Frameworks:** net48 (Revit 2019-2024), net8.0-windows (Revit 2025+)

---

## 1. Column-to-Column Splicing & Hoops
- [x] Lap Splicing (Cranked Bars) — *Implemented Feb 22*
  - Automatically calculates lap length (e.g., 40d or 50d).
  - Cranks bars inward at the top.
- [ ] Internal Cross-Ties (Diamond Ties/Links)
  - For columns wider than ~400mm, add internal cross-ties (pin bars) to secure alternating vertical bars.
- [ ] Variable Splice Locations
  - Ability to stagger the splices (e.g., crank half the bars at floor level, half at mid-height) for high-seismic zones.

## 2. Footing Starter Bars (Column Dowels)
- [x] Column Starter Bars — *Implemented Feb 22*
  - Automatically detect the column and generate L-shaped dowel bars protruding up through the footing.
- [ ] U-Bars (Edge Framing)
  - For thick footings (>500mm depth), require U-shaped edge bars to close the perimeter between top and bottom mats.

## 3. Beam Curtailment & Lapping
- [x] Bar Curtailment (L/3, L/4 rules) — *Implemented Feb 23*
  - Extra top reinforcement at supports curtailed at 1/3 or 1/4 of the clear span distance.
- [ ] Lap Splice Locations
  - Beams longer than standard stock lengths need splices. Top bars spliced at mid-span, bottom bars spliced at supports.
- [ ] Top / Bottom / Side Cover Distinction
  - Separate cover inputs for Top vs Bottom/Sides.

## 4. General Usability & "Quality of Life" Improvements
- [x] Shape Code Recognition — *Implemented*
  - Output standard shape codes (e.g., Shape Code 21, 51) into Revit schedules.
- [x] Prefix / Suffix Naming & Partitions — *Implemented*
  - Automatically assign a "Partition" or "Schedule Mark" parameter (e.g., B1-T1).
- [ ] "Number of Bars" vs "Spacing"
  - Allow inputting exact bar counts for main longitudinal bars in beams.
- [x] Clash Avoidance — *Implemented*
  - Apply a slight micro-offset to the beam's top layer to physically clear the column verticals.

## 5. Bug Fixes & Polish (Completed)
- [x] Bar Alignment (Corner + Side bars) — *Fixed Mar 2-3*
  - All main bars correctly aligned horizontally and vertically.
  - Cross ties (diamond shape) properly wrap around side bars.
- [x] Build errors (nullability, obsolete API) — *Fixed Mar 1*
  - Resolved all C# compiler errors for multi-target builds.

---

## Project File Overview

| File | Purpose |
|---|---|
| `RebarGeneratorCommand.cs` | Core Revit command — beam, column, footing generation |
| `RebarGeneratorWindow.xaml` | WPF UI layout for rebar configuration |
| `RebarGeneratorWindow.xaml.cs` | UI code-behind / event logic |
| `ProfileManager.cs` | User profile (settings) management |
| `Models/RebarPoint.cs` | Rebar point data model |
| `Models/RebarSetDef.cs` | Rebar set definition model |
| `Helpers/ElementIdHelper.cs` | Revit ElementId compatibility helper |
| `BeamSelectionFilter.cs` | Revit element selection filter |
| `ToolsByGimhanApplication.cs` | Revit add-in application entry point |
| `ToolsByGimhan.addin` | Revit add-in manifest |
