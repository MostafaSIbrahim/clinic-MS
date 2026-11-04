# Slice F9a: language switch, Arabic shell and right-to-left layout

Roadmap item **F9** (localisation plumbing), slice a of 2 (Group 2). This slice starts on **F28** (not ready for two languages) and decisions **K5** (English by default, Arabic on request) and **D10** (Western digits).

**Claude implemented this slice at the owner's request.** The owner builds, tests, checks and opens the PR.

| | |
|---|---|
| Branch | `chore/f9a-language-switch` (from the working copy, after PR #33 is merged) |
| Behaviour change | 1. **Language switch in the top bar.** It's a button that shows the other language in its own script: **العربية** while in English, **English** while in Arabic. The choice is remembered on this browser for a year. **English stays the default** for everyone, and the browser's language setting is ignored. 2. **In Arabic**, the page is marked `lang="ar"` and `dir="rtl"`. The Arabic font is used, Bootstrap's right-to-left stylesheet is loaded, and the whole shell is mirrored: the sidebar is on the right and the menus open the other way. The **sidebar** and **top bar** (search, account menu, skip link, menu buttons) are in Arabic. 3. **Page content stays in English for now**: page titles, forms, tables, the new components and enum names. Each workspace gets its Arabic text as it's migrated, as the roadmap plans. Patient notes in Arabic already show correctly, because they use `dir="auto"`. 4. **Numbers, dates and money always use one culture (`en-US`)**, in both languages. This means Western digits and English month names, and form values are read the same way whatever the language. The app previously used the server's own culture; on an `en-US` server, nothing changes |
| Not included (F9b) | Arabic text for the shared components (pagination, empty states, filter bar, confirm dialog, toasts, field errors); enum names from resources instead of `Labels.Humanize`; saving the language **per user**, which needs a database column and so a reviewed migration; a switch on the sign-in page |
| Database | None. The choice is kept in a cookie |
| Tests | +21 (about **286**). They cover: Arabic exists for every menu label and every shell string, with none blank; the resource files are really found (Arabic text in Arabic, English otherwise); English is the default and only the language cookie can change it; formatting is always `en-US`; the cookie changes only the language; the return address must be on this site; the switch sets the cookie and refuses other languages; the layout sets `lang`/`dir` and loads the right-to-left Bootstrap; `site.css` uses no left/right properties, so everything mirrors; the right-to-left Bootstrap file is present |

## Design

| Decision | Choice |
|---|---|
| Cultures | `AppCultures` (`Web/Localization`). **Formatting culture:** always `en-US`. **UI language:** `en` (default) or `ar`. The only source is the standard ASP.NET culture cookie; query strings and the browser's `Accept-Language` are not used |
| Strings | `IStringLocalizer<SharedResource>`, where the **English text is the key**. So a missing Arabic entry shows English, never a blank or a code. Arabic lives in `Resources/SharedResource.ar.resx`. The neutral `.resx` is deliberately empty |
| Switch | `POST /Language/Set` (signed-in users, anti-forgery token). It sets the cookie (HttpOnly, SameSite Lax, Secure on HTTPS, 1 year) and returns to the same page. Other sites are never allowed as the return address |
| Right-to-left | `<html lang dir>` comes from the language. Bootstrap's `bootstrap.rtl.min.css` replaces `bootstrap.min.css` in Arabic. The remaining left/right rules in `site.css` (sidebar border, menu icon gap, alert edge, skip link, stat-card decoration) became logical properties (`inline-start`/`inline-end`). The Arabic font comes from the existing token rule `:root:lang(ar)` |
| Menu | The catalogue keeps English labels. The sidebar view translates them, so permissions and tests are unchanged |

### Arabic wording: please review

I've translated the menu and shell, listed below. If the clinic uses other words, tell me and I'll change the resource file.

| English | Arabic |
|---|---|
| Dashboard | لوحة المتابعة |
| Front desk | الاستقبال |
| Patients | المرضى |
| Appointment Board | لوحة المواعيد |
| Reservations | الحجوزات |
| My Queue / Doctor Queues | قائمة انتظاري / قوائم انتظار الأطباء |
| Follow-ups | المتابعات |
| Clinical | الطبي |
| Analysis | التحاليل |
| Nutrition | التغذية |
| Billing | الحسابات |
| Unpaid Payments / Payment Dashboard | المدفوعات غير المسددة / لوحة المدفوعات |
| Payment Reports | تقارير المدفوعات |
| Administration | الإدارة |
| Users | المستخدمون |
| Permission Groups | مجموعات الصلاحيات |
| Lookups | القوائم المرجعية |
| Patient Sources | مصادر المرضى |
| Clinics | العيادات |
| Find patient: name or phone | ابحث عن مريض: الاسم أو الهاتف |
| Change password / Sign out | تغيير كلمة المرور / تسجيل الخروج |

## Files

| Action | Path |
|---|---|
| Add | `SafyaClinic.Web/Localization/AppCultures.cs`, `Localization/SharedResource.cs` |
| Add | `SafyaClinic.Web/Resources/SharedResource.resx` (empty, English), `Resources/SharedResource.ar.resx` |
| Add | `SafyaClinic.Web/Controllers/LanguageController.cs` |
| Edit | `Program.cs`: localisation services and `UseRequestLocalization()` before routing |
| Edit | `Views/Shared/_Layout.cshtml`: `lang`/`dir`, the right-to-left Bootstrap file, translated shell text, the language switch |
| Edit | `Views/Shared/Components/SidebarNav/Default.cshtml` (translated labels), `Navigation/NavigationCatalogue.cs` (`AllLabels` for tests) |
| Edit | `wwwroot/css/site.css`: left/right → logical properties (7 rules) |
| Edit | `tests/.../Security/access-matrix.txt` (+ `Language.Set POST: signed-in`), `tests/.../LocalAssetsTests.cs` |
| Add | `tests/SafyaClinic.Tests/Web/LocalisationTests.cs` |

## Rollback

Revert the PR's merge commit. Anyone who chose Arabic keeps a harmless cookie, which the reverted app ignores.

---

## Your steps

Run these in PowerShell from `D:\MyDevWork\clinic-MS`.

### Step 1: Branch

Merge **PR #33** first. Then:

```powershell
git switch -c chore/f9a-language-switch
git status
```

You should see 7 modified files, plus the new `LanguageController.cs`, the `Localization` and `Resources` folders, and `LocalisationTests.cs`.

### Step 2: Build and test

```powershell
dotnet build SafyaClinic\SafyaClinic.sln
dotnet test SafyaClinic\SafyaClinic.sln
```

Expected: about **286 passed**. Tell me the number if it differs but nothing fails.

### Step 3: Check

```powershell
dotnet run --project SafyaClinic\SafyaClinic.Web --launch-profile http
```

1. **Sign in.** Everything is in English, as before, and the top bar has an **العربية** button.
2. **Click العربية.** The same page reloads in Arabic:
   - The sidebar is on the **right**, with Arabic labels and the Arabic font.
   - The top bar search says "ابحث عن مريض: الاسم أو الهاتف".
   - The account menu opens towards the left and says تغيير كلمة المرور / تسجيل الخروج.
   - The page content itself is still English, which is expected.
3. **Check numbers stay Western in Arabic:**
   - On the Dashboard and in Payments, amounts read like `1,250.50 EGP`, and dates like `04 Nov 2026`.
   - **Save something with a decimal amount** (e.g. collect a payment of 12.50 on a test reservation). It saves correctly.
4. **Narrow window in Arabic:** ☰ opens the menu from the right, and × closes it.
5. **Sign out and back in.** Arabic is remembered. Click **English** to switch back.
6. **Another browser** (or a private window) starts in English.

### Step 4: Commit, push, PR

```powershell
git add -A SafyaClinic Docs
git status
git commit -m "F9a: language switch, Arabic shell and right-to-left layout (English default, Western digits)"
git push -u origin chore/f9a-language-switch
```

Send me the PR number and the CI result. Please also send any changes to the Arabic wording.

## Next

- **F9b:**
  - Arabic for the shared components and enum names.
  - The language saved **per user**. This needs a small migration adding `Users.PreferredLanguage`, which I'll show you for approval first.
  - The switch on the sign-in page.
- **F4d-3:** the reviewed migration that drops the old `Roles` / `UserRoles` tables.
- Then the **pilot** (Group 3).
