---
created: 2026-02-23T19:32:30.872Z
title: ClientSection (Afdeling) autocomplete hierarchical display
area: ui
files:
  - src/SignaturPortal.Web/Components/Pages/Recruiting/ActivityCreateEdit.razor
  - src/SignaturPortal.Infrastructure/Services/ErActivityService.cs
---

## Problem

The "Afdeling" (ClientSection) autocomplete on ActivityCreateEdit currently shows a flat list of sections. The legacy system renders sections grouped under their ClientSectionGroup (parent-child hierarchy) — e.g. department groups with their departments nested underneath.

The current `SearchClientSectionsAsync` method does a live DB search returning a flat `List<ClientSectionDropdownDto>` with no grouping. MudAutocomplete does not natively support grouped/hierarchical rendering.

## Solution

TBD — options to evaluate:

1. **Grouped MudAutocomplete**: Load all sections with their group name, render group headers as non-selectable items interleaved in the list (manual simulation of grouping).
2. **Custom tree component**: Replace the autocomplete with a custom MudBlazor-based tree picker dialog (similar to UserPickerDialog pattern already used on this form).
3. **Flat list with group prefix**: Prefix each section name with its group name (e.g. "Ledelse > HR") — simplest approach, no new component needed.

Check how legacy `clientSection` control renders hierarchy before deciding. The `ClientSectionGroup` → `ClientSection` relationship is the key data model (ClientSection.ClientSectionGroupId FK).
