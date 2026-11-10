# Slice P8b: drug suggestions from a list of 25,000 Egyptian medicines

A follow-up to the pilot, from the owner's manual test of 10 Nov 2026 (comment 3: "no drug name or information appear"). The owner asked for a free source that works better than what we had.

**Claude implemented this slice at the owner's request.** The owner deletes one old file, runs one command, does the manual check, and pushes.

## Why a built-in list, not an outside API

- **There's no connection to the Egyptian Drug Authority.** I found no free official service to connect to.
- **Before this slice:**
  - locally, the search called DwaPrices' free *sample* address, which DwaPrices describes as a test endpoint before subscribing;
  - the live site's settings don't name a provider, so it used a built-in list of only **33 drugs**.
- **The best free source** is the public-domain (CC0) **Egyptian Drug Database**: **25,070 medicines registered in Egypt**. Each entry has its English trade name with strength and pack, an Arabic name, the active ingredient, the maker, the drug class, the route and a price.
- **It's built into the app.** Suggestions are instant, need no key or outside service, and keep working if a website goes down.

| | |
|---|---|
| Branch | `feature/p8b-drug-list` (from an up-to-date `main`) |
| Behaviour change | **1. Drug suggestions** come from the built-in list of 25,070 medicines, on any site, with no setting needed. You can type the trade name, the Arabic name or the active ingredient, words in any order (`625 augmentin`). Arabic matches without diacritics, and أ/إ/آ/ا, ى/ي and ة/ه are treated alike. Names starting with what you typed come first, then names containing it, then other brands with the same active ingredient. **2. Each suggestion shows** the trade name (with strength and pack), the active ingredient, the price, the route, the maker and the Arabic name. Choosing one fills in the trade name, as before. **3. When nothing matches**, the list says *No matching drug in the list. You can type the name.* If the search can't be reached, it says *Drug suggestions are unavailable right now*. Before, it showed nothing. **4.** A failed lookup is no longer remembered for an hour, so the next keystroke tries again (roadmap M3). **5.** The DwaPrices sample address is no longer the default. DwaPrices remains an option if you ever subscribe (`EgyptianDrugIndex:Provider` = `DwaPrices`) |
| Caveat | Prices are a snapshot from Oct 2026, and the Arabic names are the compiler's spellings, not official ones. The suggestions help typing; the doctor still checks the name, and can always type one that isn't listed |
| Database | None |
| Size | The list is compressed (1 MB) and built into the application assembly, so a publish can't leave it behind |
| Tests | +8 (the search order; every word must match; the active ingredient finds every brand; Arabic without diacritics or hamza; result limit; and the built-in list loads over 20,000 drugs, with real Augmentin products, prices, Arabic names and ingredients) |

## Files

| Action | Path |
|---|---|
| Add | `Application/Data/egyptian-drugs.json.gz` (the list, CC0), `Application/Data/egyptian-drugs.SOURCE.md` (origin, licence, how to update), `Application/Services/EgyptianDrugIndex/DrugIndex.cs`, `tests/.../Application/DrugIndexTests.cs` |
| Edit | `Application/Services/EgyptianDrugIndex/LocalEgyptianDrugSource.cs` (rewritten), `EgyptianDrugService.cs` (failures not cached), `DTOs/Medicine/EgyptianMedicineDto.cs` (+ Arabic name, drug class), `Options/EgyptianDrugIndexOptions.cs` (comment), `SafyaClinic.Application.csproj` |
| Edit | `Web/appsettings.json` (Provider `Local`), `Web/wwwroot/js/medicine-autocomplete.js`, `Views/MedicalRecords/CreatePrescription.cshtml`, `PrescriptionDetails.cshtml`, `Resources/SharedResource.ar.resx` (+2) |
| **Delete** (you, Step 1) | `Application/Data/egyptian-medicines-seed.json` (the old 33-drug list) |

## Rollback

Revert the PR's merge commit.

## On the live site

Publish as usual. There's no migration and no server setting to change: the server's `appsettings.json` has no drug provider, so it uses the built-in list.

---

## Your steps

**Step 1.** Delete the old list:

```powershell
cd D:\MyDevWork\clinic-MS
git switch main
git pull
Remove-Item SafyaClinic\SafyaClinic.Application\Data\egyptian-medicines-seed.json
```

**Step 2.**

```powershell
.\.assessment\slice.ps1 -Branch feature/p8b-drug-list `
  -Message 'P8b: drug suggestions from a built-in list of 25,070 Egyptian medicines (CC0); clear messages when nothing matches'
```

### Manual check (while the app runs)

As a doctor, open a test patient's record → **New prescription**, and type in **Medication**:

1. `aug`: Augmentin products appear, such as *AUGMENTIN 1 GM 14 F.C.TABS.*, each with its ingredient, price and the Arabic name. Choose one: the field fills in.
2. `625 augmentin`: only the 625 mg product.
3. `amoxicillin`: brands with that ingredient (Augmentin, Hibiotic, …).
4. `بنادول` or `اوجمنتين` (Arabic, without the hamza): matching products.
5. `zzzz`: *No matching drug in the list. You can type the name.* Type any name and save: it's accepted.
6. On an existing prescription's page, the drug field gives the same suggestions.
7. In Arabic (العربية), the message in step 5 is in Arabic.

When you've finished, press Enter in the script, then type `YES` to commit and push. Open the PR and send me its number and the CI result.

## Next: Group 4, M1 patient workspace

One patient page with tabs: Overview, Visits, Records, Lab, Nutrition, Payments and Timeline. The separate patient pages merge into it, and their old addresses redirect. I'll send the plan first, because it changes where staff find things.
