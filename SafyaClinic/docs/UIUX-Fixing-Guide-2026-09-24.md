# UI/UX review and fixing guide

Reviewed 24 September 2026. Root: `D:\MyDevWork\clinic-MS\SafyaClinic`. All paths below are relative to that root.

## Review result and scope

The implementation is substantially in place, but it is **not ready for final UI/UX sign-off**. This review inspected current views, controller changes, shared JavaScript/CSS and the earlier implementation guide. No build, live browser test, database operation or application-source edit was performed. Visual behavior still needs your tests. The snippets below are proposed edits, not compiled changes.

Correctly present in the inspected source: contact modals and shared submit helper; address validation and contact-removal feedback; patient workspace shortcuts; payment-service-based reservation collection card and role-aware completion redirect; responsive payment cards/history; persistent error/warning alerts; sidebar list semantics; queue filters; clinical field grouping.

The findings include omissions from the last guide and older workflow defects exposed by this review. Older defects are labelled; they are not attributed to your latest changes.

| ID | Priority | Finding | Origin |
| --- | --- | --- | --- |
| F1 | P1 | Editing a selected patient's search text leaves the old hidden PatientId; search responses can arrive out of order; popup messages lack origin/source checks | Existing booking workflow |
| F2 | P2 | Shared payment checkbox still opens collection using only IsPaid, including for roles/states that cannot collect | Existing shared partial, missed by previous guide |
| F3 | P2 | Pagination still discards search/date/category filters | Earlier guide Step 3 not implemented |
| F4 | P2 | Clinic validation span/helper text placed inside select on Collect | New markup error |
| F5 | P2 | Reservation Details has an extra unclosed card wrapper around Payment/Clinical | New markup error |
| F6 | P2 | Clinical Create repeats validation summary; several forms have ModelOnly summaries without field messages | Duplicate is new; missing field feedback is broader unfinished work |
| F7 | P2 | Patient search label points to itself; reservation filter labels do not match input IDs | New accessibility errors |
| F8 | P3 | Medical record identity/lock indicator and Back to Queue removed; empty More Actions for locked records | New workflow/readability regression |
| F9 | P3 | Remaining headings, action wrapping and table-region labels are inconsistent | Partial implementation |
| F10 | P2/P3 | Analysis/Nutrition form finish remains; failed analysis submit loses checked selection; discount input forces zero on redisplay | Existing forms, unfinished pass |

Implement in this order. Build/test/checkpoint each fix. Preserve server authorization, reservation/queue state validation, payment transactions and all antiforgery tokens. Do not run migrations or alter demo data for these fixes.

## F1 — make patient selection reliable before further booking UI work

**Reproduce first:** open Reservations/Create, select patient A, replace their displayed name with another name without selecting a result, then inspect patientIdHidden. It still contains A. A valid-looking visible name can therefore book against the wrong patient. This is the highest-impact finding.

### F1.1 — update the Create view hooks

File: `SafyaClinic.Web/Views/Reservations/Create.cshtml`.

1. Add `id="reservationCreateForm"` to the existing Create form. Keep its action, POST and token.
2. Replace the Patient label with `<label for="patientSearchInput" class="form-label">Patient (required)</label>`.
3. Keep the hidden PatientId and visible search input IDs unchanged. Add the following attributes to the visible input:

```cshtml
data-search-url="@Url.Content("~/api/patients/search")"
aria-describedby="patientSearchHelp patientSearchStatus"
```

4. Replace the existing “Type at least 2 characters…” small element with:

```html
<small id="patientSearchHelp" class="text-muted">Type at least 2 characters, then select a patient from the results.</small>
<div id="patientSearchStatus" class="small mt-1" role="status" aria-live="polite"></div>
<span asp-validation-for="PatientId" class="text-danger"></span>
```

5. On Register New Patient, remove `onclick="openNewPatientWindow()"`, add `id="registerPatientButton"` and `data-popup-url="@Url.Action("Create", "Patients", new { popup = true })"`. Keep type="button".
6. Inside Scripts delete the entire Patient Autocomplete IIFE (from its comment through its matching `})();`). Delete the New Patient Popup function and message listener at the end. Keep the Treatment Type Auto-fill listener unchanged.
7. After the remaining inline script add:

```cshtml
<script src="~/js/reservation-patient-picker.js" asp-append-version="true"></script>
```

### F1.2 — add the complete replacement script

Create `SafyaClinic.Web/wwwroot/js/reservation-patient-picker.js`:

```javascript
(() => {
    const form = document.getElementById('reservationCreateForm');
    if (!form) return;
    const input = document.getElementById('patientSearchInput');
    const hidden = document.getElementById('patientIdHidden');
    const status = document.getElementById('patientSearchStatus');
    const register = document.getElementById('registerPatientButton');
    const list = document.createElement('div');
    list.id = 'patientSearchResults';
    list.className = 'list-group shadow-sm position-absolute w-100';
    list.style.cssText = 'z-index:1050;max-height:260px;overflow-y:auto';
    list.setAttribute('role', 'listbox');
    list.setAttribute('aria-label', 'Matching patients');
    list.hidden = true;
    input.parentElement.appendChild(list);
    input.setAttribute('role', 'combobox');
    input.setAttribute('aria-autocomplete', 'list');
    input.setAttribute('aria-controls', list.id);
    input.setAttribute('aria-expanded', 'false');
    let timer, request, revision = 0, popup = null, items = [], active = -1;

    function close() {
        list.hidden = true;
        list.replaceChildren();
        items = []; active = -1;
        input.setAttribute('aria-expanded', 'false');
        input.removeAttribute('aria-activedescendant');
    }
    function invalidateRequest() {
        clearTimeout(timer);
        request?.abort();
        revision++;
    }
    function validateSelection() {
        const valid = Number.isSafeInteger(Number(hidden.value)) && Number(hidden.value) > 0
            && input.value.trim().length > 0;
        input.setCustomValidity(valid ? '' : 'Select a patient from the search results.');
        return valid;
    }
    function select(patient) {
        const id = Number(patient?.id);
        if (!Number.isSafeInteger(id) || id <= 0 || typeof patient.fullName !== 'string') return;
        invalidateRequest();
        hidden.value = String(id);
        input.value = patient.fullName + (patient.primaryPhone ? ' — ' + patient.primaryPhone : '');
        validateSelection(); close();
        status.textContent = 'Selected patient #' + id + ': ' + patient.fullName;
        input.focus();
    }
    function highlight(index) {
        active = index;
        [...list.children].forEach((el, i) => {
            el.classList.toggle('active', i === index);
            el.setAttribute('aria-selected', String(i === index));
        });
        if (list.children[index]) {
            input.setAttribute('aria-activedescendant', list.children[index].id);
            list.children[index].scrollIntoView({ block: 'nearest' });
        }
    }
    function render(results) {
        close();
        items = results.filter(p => Number.isSafeInteger(Number(p.id))
            && Number(p.id) > 0 && typeof p.fullName === 'string');
        if (!items.length) { status.textContent = 'No matching patients. Try another search or register a patient.'; return; }
        items.forEach((p, i) => {
            const option = document.createElement('button');
            option.type = 'button'; option.tabIndex = -1;
            option.id = 'patientOption-' + i;
            option.className = 'list-group-item list-group-item-action text-start';
            option.setAttribute('role', 'option');
            option.setAttribute('aria-selected', 'false');
            option.textContent = p.fullName + ' · #' + p.id +
                (p.primaryPhone ? ' · ' + p.primaryPhone : '') +
                (p.nationalId ? ' · ID: ' + p.nationalId : '');
            option.addEventListener('mousedown', e => e.preventDefault());
            option.addEventListener('click', () => select(p));
            list.appendChild(option);
        });
        list.hidden = false;
        input.setAttribute('aria-expanded', 'true');
        status.textContent = items.length + ' matches. Use arrow keys and Enter to select.';
    }
    input.addEventListener('input', () => {
        invalidateRequest(); close();
        hidden.value = '';
        validateSelection();
        const term = input.value.trim();
        if (term.length < 2) { status.textContent = 'Type at least 2 characters.'; return; }
        const current = revision;
        status.textContent = 'Searching…';
        timer = setTimeout(async () => {
            const controller = new AbortController(); request = controller;
            try {
                const url = new URL(input.dataset.searchUrl, location.origin);
                url.searchParams.set('query', term);
                const response = await fetch(url, { signal: controller.signal,
                    credentials: 'same-origin', headers: { Accept: 'application/json' } });
                if (!response.ok || response.redirected ||
                    !(response.headers.get('content-type') || '').includes('application/json'))
                    throw new Error('Search unavailable');
                const results = await response.json();
                if (current !== revision || input.value.trim() !== term || document.activeElement !== input) return;
                if (!Array.isArray(results)) throw new Error('Invalid response');
                render(results);
            } catch (error) {
                if (error.name === 'AbortError' || current !== revision) return;
                close(); status.textContent = 'Patient search is unavailable. Check your connection/sign-in, then try again.';
            }
        }, 300);
    });
    input.addEventListener('keydown', e => {
        if (e.key === 'Escape') { invalidateRequest(); close(); return; }
        if (list.hidden) return;
        if (e.key === 'ArrowDown') { e.preventDefault(); highlight(Math.min(active + 1, items.length - 1)); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); highlight(Math.max(active - 1, 0)); }
        else if (e.key === 'Enter') {
            e.preventDefault();
            if (active >= 0) select(items[active]);
            else status.textContent = 'Choose a result with the arrow keys or click a patient.';
        }
    });
    input.addEventListener('blur', () => setTimeout(() => {
        if (!list.contains(document.activeElement)) { invalidateRequest(); close(); }
    }, 150));
    form.addEventListener('submit', e => {
        if (!validateSelection()) { e.preventDefault(); input.reportValidity(); input.focus(); }
    });
    register.addEventListener('click', () => {
        popup = window.open(register.dataset.popupUrl, 'NewPatient',
            'width=900,height=700,scrollbars=yes,resizable=yes');
        if (!popup) status.textContent = 'Allow popups to register a patient, then try again.';
    });
    window.addEventListener('message', e => {
        if (e.origin !== location.origin || !popup || e.source !== popup ||
            e.data?.type !== 'NEW_PATIENT_CREATED') return;
        select(e.data.patient);
    });
    validateSelection();
    if (Number(hidden.value) > 0) status.textContent = 'Selected patient #' + hidden.value;
})();
```

Keep server validation. The script prevents accidental mismatches; a hidden ID is not an authorization boundary.

### F1.3 — encode and restrict the popup's reply

File: `Views/Patients/CreateSuccessPopup.cshtml`. Replace its complete script block with the following. Keep Layout=null and all visual markup unchanged:

```cshtml
<script>
    const createdPatient = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(new {
        id = ViewBag.NewPatientId,
        fullName = ViewBag.NewPatientName,
        primaryPhone = ViewBag.NewPatientPhone
    }));
    function sendAndClose() {
        if (window.opener) {
            window.opener.postMessage({ type: 'NEW_PATIENT_CREATED', patient: createdPatient },
                window.location.origin);
        }
        window.close();
    }
    setTimeout(sendAndClose, 3000);
</script>
```

Use the default System.Text.Json encoder, not UnsafeRelaxedJsonEscaping. Html.Raw here surrounds JSON serialized with that encoder; do not use it on raw patient text.

**Test:** preselected patient opens correctly; select A then type B clears ID immediately; raw text cannot submit; valid B sets B's ID; fast typing never displays obsolete results; shortening input clears results; keyboard and mouse work; blank/no-match/network-error states are distinct; popup creates/selects correct patient; apostrophes and Arabic names display correctly; unrelated messages are ignored; existing treatment autofill and server validation still work.

## F2 — remove the misleading shared payment action

File: `Views/Shared/_PaymentStatusToggle.cshtml`. Used by Dashboard, Reservations/Today and Reservations/Details. The new Details collection card is correct, but this separate checkbox still offers a route to Collect when IsPaid=false. Backend collection checks remain; this is misleading UI, not evidence that an invalid payment succeeds.

Replace the entire partial with this compatible tuple-based implementation:

```cshtml
@model (int ReservationId, int PatientId, bool IsPaid)
<span class="badge @(Model.IsPaid ? "bg-success" : "bg-secondary")">
    @(Model.IsPaid ? "Covered" : "Not fully covered")
</span>
@if (User.IsInRole("Admin") || User.IsInRole("Reception"))
{
    <a asp-controller="Payments" asp-action="PatientSummary"
       asp-route-patientId="@Model.PatientId"
       class="btn btn-sm btn-outline-secondary ms-1">Payment summary</a>
}
```

Keep the partial name and every caller unchanged for now. This status is coverage, not collectible eligibility. Rename the adjacent “Paid” column/description to “Payment coverage” on the three active callers. The summary and Details card already provide correct collectible actions; do not add per-row database calls in Razor. “Covered” also avoids misrepresenting a write-off as cash paid.

**Test:** Pending/Waiting no longer offer direct Collect through the checkbox; doctors have no payment link; reception can inspect summary; InConsultation/Finished positive balances retain the authoritative collection buttons; write-off/partial/zero balances still behave as before.

## F3 — implement the missing pagination replacement

Replace `Views/Shared/_Pagination.cshtml` in full with the code below. Preserve callers' PagedResult<object> conversion and PaginationAction. These current filters are single-valued.

```cshtml
@model PagedResult<object>
@using Microsoft.AspNetCore.Routing
@{
    var action = ViewData["PaginationAction"]?.ToString() ?? "Index";
    string PageUrl(int page)
    {
        var values = new RouteValueDictionary();
        foreach (var pair in Context.Request.Query)
        {
            if (pair.Key.Equals("page", StringComparison.OrdinalIgnoreCase) ||
                pair.Key.Equals("pageSize", StringComparison.OrdinalIgnoreCase)) continue;
            values[pair.Key] = pair.Value.ToString();
        }
        values["page"] = page;
        values["pageSize"] = Model.PageSize;
        return Url.Action(action, values) ?? "#";
    }
}
@if (Model != null && Model.TotalPages > 1)
{
    <nav aria-label="Results pages" class="mt-3">
        <ul class="pagination justify-content-center flex-wrap gap-1">
            <li class="page-item @(!Model.HasPrev ? "disabled" : "")">
                @if (Model.HasPrev)
                {
                    <a class="page-link" href="@PageUrl(Model.Page - 1)">Previous</a>
                }
                else { <span class="page-link" aria-disabled="true">Previous</span> }
            </li>
            @for (var page = Math.Max(1, Model.Page - 2);
                  page <= Math.Min(Model.TotalPages, Model.Page + 2); page++)
            {
                <li class="page-item @(page == Model.Page ? "active" : "")">
                    @if (page == Model.Page)
                    {
                        <span class="page-link" aria-current="page">@page</span>
                    }
                    else { <a class="page-link" href="@PageUrl(page)">@page</a> }
                </li>
            }
            <li class="page-item @(!Model.HasNext ? "disabled" : "")">
                @if (Model.HasNext)
                {
                    <a class="page-link" href="@PageUrl(Model.Page + 1)">Next</a>
                }
                else { <span class="page-link" aria-disabled="true">Next</span> }
            </li>
        </ul>
        <p class="small text-muted text-center">Page @Model.Page of @Model.TotalPages</p>
    </nav>
}
```

**Test:** Patients search followed by Next keeps search; reservations retains dates/category; Previous works; new filter submissions start at page 1; disabled boundaries cannot navigate; several dozen pages do not overflow mobile. Search the solution for `_Pagination` and test all active callers. Do not change the separate audit modal's pagination unless it has the same confirmed defect.

## F4 — fix collection form markup

File: `Views/Payments/Collect.cshtml`.

Inside `<select asp-for="ClinicId" ...>` delete the placeholder small element containing “…first visit… deduction…” and remove the validation span from inside that select. Keep the empty option and clinics loop. Immediately after the matching `</select>` insert:

```cshtml
<span asp-validation-for="ClinicId" class="text-danger"></span>
```

Keep the existing real deduction helper paragraph after it. Only option/optgroup content belongs in this ordinary select; the current browser parsing can discard the validation element.

Wrap the existing final Collect Payment button and Cancel anchor in `<div class="form-actions">…</div>`, still inside the form. Do not wrap any surrounding form. Keep the hidden IDs, token, Amount binding, clinic selection and POST action unchanged.

**Test:** inspect the rendered DOM: ClinicId message is a sibling after the select, not a child. Invalid clinic displays error; other errors retain entered values; reservation clinic remains selected; payment is recorded once after correction.

## F5 — remove the stray reservation card wrapper

File: `Views/Reservations/Details.cshtml`. Near the end, the current structure starts:

```cshtml
        <div class="card mb-3">
            @if (User.IsInRole("Admin") || User.IsInRole("Reception"))
                {
                    <div class="card mb-3">
```

Delete **only the first opening `<div class="card mb-3">` immediately before this payment-role if**. Keep the inner card inside the if. Do not remove either of the final two closing divs: they close the right column and outer row after this correction. The Clinical card should be a sibling after the payment if, within `col-md-5`.

Expected nesting:

```text
row
  col-md-7: reservation details
  col-md-5
    Update Status card
    if Admin/Reception: Payment card
    if Doctor/Admin: Clinical card
```

**Test:** admin, reception and doctor renders have no blank outer card/double border; both columns close inside the page content; footer/layout are not nested in a card; resize to phone width; existing forms/routes still work.

## F6 — make validation failures visible

In `Views/MedicalRecords/Create.cshtml`, two identical ModelOnly summaries exist (before and after hidden IDs). Delete the second. Change the remaining summary to:

```cshtml
<div asp-validation-summary="All" class="alert alert-danger py-2"
     role="alert" tabindex="-1"></div>
```

Use one All summary with these attributes in each of: MedicalRecords/Edit, Patients/Create, Patients/EditBasic and Patients/EditMedical. They currently lack field-level message spans; ModelOnly omits errors keyed to an individual property. A malformed date/number can therefore return the form without a readable explanation. Do not remove server validators or clear ModelState.

For clinical Create/Edit, add a span immediately after each corresponding control:

```cshtml
<span asp-validation-for="FollowUpDate" class="text-danger"></span>
```

Repeat for ChiefComplaint, PresentIllnessHistory, Diagnosis, DifferentialDiagnosis, TreatmentPlan and Notes; also Category on Create. Use the actual property name for each. Keep the spans outside textarea/select elements. You may add matching spans on patient forms the same way; the All summary is the minimum complete fix.

**Test:** a server-rejected value displays a message, entered clinical text stays intact, Create shows one summary not two; valid submit succeeds. Test actual server errors as well as native browser required-field prompts.

## F7 — repair labels and filter reset

`Views/Patients/Index.cshtml`: replace `<label for="patientsearch" class="form-label" id="patientsearch">Search</label>` with `<label for="patientSearch" class="form-label">Name, phone or national ID</label>`. Add `id="patientSearch"` to the adjacent search input. Keep name/value. Give the form `asp-action="Index"` and keep method=get.

`Views/Reservations/Index.cshtml`: change label for values according to this table; input IDs already use the right-hand values:

| Current for | Replace with |
| --- | --- |
| DateFrom | reservationDateFrom |
| DateTo | reservationDateTo |
| Category | reservationCategory |

Add `asp-action="Index"` and use `class="row g-3 align-items-end mb-3 filter-panel"` on its GET form. After Filter, add `<a asp-action="Index" class="btn btn-outline-secondary">Clear</a>`. Change the no-results text to “No reservations match these filters.” (one period).

Add a visible label for the status select in Reservations/Details, e.g. `for="reservationStatus"`, and add that ID to the select. Keep name=statusId and all eligibility attributes.

**Test:** clicking every label focuses its input; screen-reader names are meaningful; Clear removes date/category query values; pagination from F3 continues retaining active filters.

## F8 — restore clinical workspace context

File: `Views/MedicalRecords/Details.cshtml`.

1. Replace `<h1 class="mb-0">Medical Record</h1>` with:

```cshtml
<h1 class="mb-0">Medical record #@Model.Id</h1>
<span class="badge @(Model.IsLocked ? "bg-secondary" : "bg-success")">
    @(Model.IsLocked ? "Locked" : "Editable")
</span>
```

2. Keep Back to Patient. Add this sibling in page-actions to restore the clinician's queue return:

```cshtml
<a asp-controller="Reservations" asp-action="Queue"
   class="btn btn-outline-secondary">Back to Queue</a>
```

3. Wrap the entire More Actions dropdown div in `@if (!Model.IsLocked) { ... }`. Do not include Complete Consultation or other forms in this wrapper. The existing Edit and Admin Lock conditions stay. Locked records currently render an empty dropdown.
4. Remove the placeholder `id="...no..."` from the Clinical Notes h2. Keep record-clinical on its container.
5. Remove the span surrounding the Prescriptions h2; put h2 directly inside the card-header.
6. Rename “OrderAnalyses” to “Order analyses”. Replace the h2 inside the Request Medical Analysis anchor with plain icon/text; an action is not a section heading.
7. Move `id="record-analyses"` from the order-action card to the card headed “Analyses from this Record”. The Analyses shortcut should reach existing results, not merely the request button. Keep request action and existing analysis links.
8. Add visible “Open analysis” text to the eye-only analysis link. Keep its controller/action/id.

**Test:** record number and lock state visible; locked record has no empty menu; section shortcuts land correctly; Back to Queue works; prescription print/analysis/attachment actions and completion conditions unchanged.

## F9 — finish small presentation gaps

These are polish items, not changes to business behavior.

### List headers

Patients/Index and Reservations/Index still use nonwrapping card headers and span titles. Keep the card design if you prefer it. Change their header class to `card-header page-toolbar mb-0`, use a direct `<div>` for the heading/description, and put the title in `<h1 class="mb-1">Patients</h1>` or Reservations. Use page-actions on the action wrapper; remove its conflicting plain nonwrapping d-flex gap-2 wrapper. Do not nest a p or h1 inside a span. Preserve all anchor routes.

In MedicalRecords/PatientRecords, remove the span around its h1; place the icon inside h1. Keep the New Record link.

### Summary tables and actions

Payments/PatientSummary: the **first** table wrapper under Ready for collection is currently labelled “Payment history”. Change it to `aria-label="Ready for collection"`; leave the second history wrapper unchanged. Change its blank th to `<th scope="col"><span class="visually-hidden">Actions</span></th>`.

Payments/Report: add `tabindex="0" role="region" aria-label="Payment report results"` to its table-responsive wrapper. Keep audit popup markup/script targets wherever they already exist; Report did not gain a new audit action in this pass and this guide does not invent one.

MedicalRecords/Create and Edit: add a page-toolbar h1 before the card, retaining the current ViewData title. Remove the redundant card title text or make it a section title; keep the form. Add a shared `.clinical-form-card { max-width: 50rem; }` rule and replace their inline width with that class if desired.

**Test:** no duplicate h1 per page; 360px layout wraps buttons; table regions have distinct names; keyboard focus and print remain usable. Keep current design tokens rather than adding another palette.

## F10 — finish the remaining Analysis/Nutrition forms

These forms were not changed in your current diff. They are remaining scope from the earlier guide, not proof that they fail all runtime tests.

### Analysis request: retain selections on a failed submit

In `Views/Analysis/Request.cshtml`, add to each AnalysisTypeIds checkbox:

```cshtml
checked="@(Model.AnalysisTypeIds.Contains(t.Id))"
```

Keep name, id and value. Change its one summary to All as in F6. Use a fieldset/legend around the analysis-type checkbox group instead of the unassociated outer label: legend text “Analysis types (select all that apply)”. Keep each checkbox's existing matching label.

Add page-toolbar with h1 “Request medical analysis” before the card. Wrap existing Request/Cancel controls in form-actions. When Model.RecordId has a value, Cancel should use MedicalRecords/Details with that id; otherwise preserve Patients/Details with Model.PatientId. This retains consultation context.

### Nutrition enrollment: stop overwriting submitted discount

In `Views/Nutrition/Enroll.cshtml`, remove only the hard-coded `value="0"` on `asp-for="DiscountPercent"`. Let the tag helper use the model/ModelState value so invalid POST redisplay does not silently show zero. Keep the default set by the DTO/controller and the existing discount/price-preview script.

Give Enroll and RecordFollowUp one page-toolbar/h1, one All validation summary, and form-actions around their current final buttons. Keep existing form routes (including enrollmentId), hidden FollowUpId and patient/package/doctor IDs. Do not rename FollowUpId during a styling change. Keep all existing field names and measurement units.

**Test:** failed analysis submit retains checked types; Cancel returns to the originating record when linked; failed enrollment retains discount/notes/package; successful enrollment price follows the existing server calculation; follow-up measurements persist after failed validation; no duplicate submit/form nesting. Treat any discovered controller failure to repopulate option lists as a separate review item rather than removing validation.

## Fix acceptance log

| Fix | Build | Test result / notes |
| --- | --- | --- |
| F1 Patient identity and popup | | |
| F2 Payment status UI | | |
| F3 Pagination | | |
| F4 Collect markup | | |
| F5 Reservation structure | | |
| F6 Validation | | |
| F7 Labels/filters | | |
| F8 Clinical context | | |
| F9 Presentation finish | | |
| F10 Remaining forms | | |

Then use `UIUX-Next-Step-Reservation-Workflow-2026-09-24.md`. Do not skip failed fixes to begin the next step. If an anchor differs from your current file, send the exact block for review. Preserve your last working checkpoint and revert only the failed edit when necessary. No database reset is part of this procedure.
