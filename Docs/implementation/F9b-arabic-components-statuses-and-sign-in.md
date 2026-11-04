# Slice F9b: Arabic for shared components, statuses, patient header and sign-in

Roadmap item **F9** (localisation plumbing), slice b. This slice continues **F28**.

**Claude implemented this slice at the owner's request.** The owner builds, tests, checks and opens the PR.

| | |
|---|---|
| Branch | `chore/f9b-arabic-components` (from the working copy, after PR #34 is merged) |
| Behaviour change | In Arabic, these now show in Arabic (English is unchanged): 1. **The sign-in page** gets the language switch, and its heading, labels and button are translated. Validation messages stay English for now. 2. **Confirm dialog:** يرجى التأكيد with إلغاء and نعم، متابعة. 3. **Toasts:** the close button's name. The message text itself comes from each screen and stays English until that screen is migrated. 4. **Pagination:** عرض 21–40 من 132, السابق / التالي, and page names for screen readers. 5. **Filter bar:** بحث / مسح. 6. **Status badges:** for example في الانتظار, في الكشف, مكتمل, مدفوع, مستحق, مشطوب. 7. **Patient header:** الحساسية, لم يُسجَّل شيء, فصيلة الدم, الأمراض المزمنة, age as "40 سنة", sex as ذكر / أنثى. **Allergy text is never translated**; it's shown exactly as recorded |
| Not included | Saving the language **per user** (F9c). It needs a new column, so I'll prepare a migration for you to review and approve before anything touches the database. Page content (titles, forms, tables, messages) is translated as each workspace is migrated |
| Database | None |
| Tests | +5 (about **290**). They cover: every `T["…"]` string in **any** view has Arabic; every status badge has an Arabic label; the filter-bar and patient-header words have Arabic; Arabic text keeps the same `{0}` placeholders as the English; the sign-in page has the language switch |

## Design

| Decision | Choice |
|---|---|
| Language switch | Moved into a partial, `_LanguageSwitch`, used by the top bar and the sign-in page. `Language/Set` is now open to **everyone** (`[AllowAnonymous]`), so it works before signing in. The anti-forgery token is still required, and the return address must still be on this site |
| Status labels | `_StatusBadge` translates `Labels.Humanize(status)`, so "InConsultation" → "In Consultation" → في الكشف. `StatusStyles.Known` lists the statuses for the test |
| Numbers in Arabic text | Pagination numbers are formatted with Western digits *before* they go into the Arabic sentence (D10) |
| Patient header | The model also carries `Age` and `Sex`, so the view can word them in the page's language. `AgeAndSex` stays as it was |
| Filter bar | Gets its words from the app's translations when it runs in a page. In unit tests it falls back to English |

### Arabic wording: please review

| English | Arabic |
|---|---|
| Please confirm · Cancel · Yes, continue | يرجى التأكيد · إلغاء · نعم، متابعة |
| Showing 21–40 of 132 · Previous · Next | عرض 21–40 من 132 · السابق · التالي |
| Search · Clear | بحث · مسح |
| Pending · Confirmed · Completed · Cancelled · No Show | معلّق · مؤكد · مكتمل · ملغي · لم يحضر |
| Not Checked In · Waiting · In Consultation · Finished · Left | لم يسجّل الحضور · في الانتظار · في الكشف · انتهى · غادر |
| Covered · Paid · Due · Not fully covered · Write Off | مسدَّد · مدفوع · مستحق · غير مسدَّد بالكامل · مشطوب |
| Allergies · none recorded · Blood type · Chronic diseases | الحساسية · لم يُسجَّل شيء · فصيلة الدم · الأمراض المزمنة |
| Sign in to your account · Phone Number · Password · Sign In | سجّل الدخول إلى حسابك · رقم الهاتف · كلمة المرور · تسجيل الدخول |

## Files

| Action | Path |
|---|---|
| Add | `Views/Shared/_LanguageSwitch.cshtml` |
| Edit | `Views/Shared/_Layout.cshtml` (uses the partial), `Views/Auth/Login.cshtml` |
| Edit | `Views/Shared/_ConfirmDialog.cshtml`, `_Toasts.cshtml`, `_AppPagination.cshtml`, `_StatusBadge.cshtml`, `Components/PatientHeader/Default.cshtml` |
| Edit | `Controllers/LanguageController.cs` (`[AllowAnonymous]` on `Set`), `Components/StatusStyles.cs`, `Components/PatientHeader.cs`, `TagHelpers/LayoutTagHelpers.cs` (filter bar) |
| Edit | `Resources/SharedResource.ar.resx` (+41 entries) |
| Edit | `tests/.../access-matrix.txt` (`Language.Set POST: anyone`), `tests/.../LocalisationTests.cs` |

## Rollback

Revert the PR's merge commit.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1: Branch

Merge **PR #34** first. Then:

```powershell
git switch -c chore/f9b-arabic-components
git status
```

### Step 2: Build and test

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: about **290 passed**.

### Step 3: Check

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

1. **Sign-in page:**
   - Sign out. The sign-in card has an **العربية** button at the top.
   - Click it. The card switches to Arabic (right-to-left), and you can still sign in.
2. **In Arabic:**
   - **Patients:** search, and check the filter bar says بحث / مسح and the pager reads عرض 1–20 من N.
   - **Dashboard / Reservations → Today:** the coverage badges read مسدَّد / غير مسدَّد بالكامل.
   - **A medical record** of a patient with allergies: the header says **الحساسية:** followed by the allergy text exactly as recorded, with the age as "N سنة".
   - **Delete or cancel something:** the dialog says يرجى التأكيد with إلغاء / نعم، متابعة.
3. **Back in English:** everything reads as before.

### Step 4: Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F9b: Arabic for shared components, status badges, patient header and the sign-in page"
git push -u origin chore/f9b-arabic-components
```

Send me the PR number and the CI result, plus any changes to the Arabic wording.

## Next

1. **F9c: language saved per user.** I'll first show you the migration script, which adds one nullable column, `Users.PreferredLanguage`, and asks for your approval before it's applied anywhere.
2. **F4d-3:** the reviewed migration that drops the old `Roles` / `UserRoles` tables. Both migrations can go in one review if you prefer.
3. Then the **pilot** (Group 3).
