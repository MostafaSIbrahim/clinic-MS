# Safya Clinic — remaining UI/UX implementation guide

Prepared 20 September 2026 against the current workspace. Project root: `D:\MyDevWork\clinic-MS\SafyaClinic`.

This is a finite plan for completing the current UI/UX pass, not a new feature backlog. The order and implementation details below are proposed for the current code. They are not quotations or mandatory instructions from OptimizationPlan.docx. No application source was modified to prepare this guide.

## Review of the step you just implemented

**Add Phone popup: no blocking finding in the inspected code.** The modal is outside other forms; the POST has antiforgery protection; controller validation covers patient ID, number length and supported phone types; validation failures stay in the popup; entered values remain available; the submit guard prevents a second click while the request is running. A redirected/non-JSON response does not get treated as a confirmed save. Error messages use textContent rather than HTML injection.

Review covered `SafyaClinic.Web/Views/Patients/Details.cshtml` and the AddPhone action in `SafyaClinic.Web/Controllers/PatientsController.cs`. This is source inspection, not a build or a browser test. Before continuing, test blank/overlong numbers, all three types, double-clicking Save, closing while saving, keyboard navigation, and an expired login. After a connection error, refresh and inspect the phone list before retrying: the browser cannot know whether the server saved before the connection failed.

Two adjacent issues are addressed below: shared pagination drops active filters; Reservation Details still bases its collection link on IsPaid rather than authoritative collectible balance and role. These do not invalidate the phone-popup step.

## How to use this guide

1. Start from your currently working version and create a source-control checkpoint.
2. Complete one numbered step at a time. Do not paste every step before testing.
3. Paths below are relative to the project root above. “Replace” means replace that exact block, not append a second copy. “Add” means insert once.
4. Build the solution after each step involving Razor or C#. Then run that step's tests. For CSS/JS, hard-refresh the browser before testing.
5. Record PASS/FAIL and a short note in the implementation log at the end. Keep a separate commit/checkpoint per passing step.
6. If an anchor no longer matches your source, stop that edit and request review with the file and surrounding code. Do not guess at a large replacement.

No database migration, package upgrade, seed update, payment backfill, or demo-data deletion is required by this plan. Keep the existing Bootstrap version and violet/teal design tokens.

### Rules that every step must preserve

| Event/state | Required behavior |
| --- | --- |
| Check In | Reservation Confirmed; queue Waiting |
| Waiting | Reservation remains Confirmed; no collectible consultation charge yet |
| Start Consultation | Queue InConsultation; reservation remains Confirmed |
| Complete reservation/consultation | Only through existing server validation of an active consultation; reservation Completed and queue Finished together |
| Collection | Positive authoritative balance for InConsultation or Finished; preserve existing exclusion/reconciliation rules |
| Finished with unpaid balance | Balance remains collectible |
| Completed reservation | Editing remains disabled and server-protected |
| Doctor queues | Doctor sees own queue; reception/admin can filter by clinic and doctor |
| Patient timeline/history report | Open in a new tab, with rel="noopener" |
| Payment audit | Remains a styled popup |
| Authorization | Existing server policies, ownership checks and transactions remain authoritative |

Do not move completion to Check In or Waiting. Do not add a separate client-side “Finish queue” operation. Never recalculate money in JavaScript or infer an amount due from IsPaid.

## Step 1 — reusable contact-popup behavior and Add Address

**Result:** Add Address behaves like Add Phone, without duplicating the submit implementation. This helper is only for the two small contact forms; do not attach it to payment or clinical forms.

### 1A. Add the helper

Create `SafyaClinic.Web/wwwroot/js/contact-modal-forms.js` with this entire content:

```javascript
(() => {
    document.querySelectorAll('form[data-contact-modal]').forEach(form => {
        if (form.dataset.bound === 'true') return;
        const modal = form.closest('.modal');
        const errorBox = form.querySelector('[data-modal-error]');
        const submit = form.querySelector('button[type="submit"]');
        if (!modal || !errorBox || !submit) return;
        form.dataset.bound = 'true';
        const originalText = submit.textContent;
        let saving = false;
        modal.addEventListener('shown.bs.modal', () => {
            form.querySelector('[data-modal-focus]')?.focus();
        });
        modal.addEventListener('hide.bs.modal', event => {
            if (saving) event.preventDefault();
        });
        form.addEventListener('submit', async event => {
            event.preventDefault();
            if (saving || !form.reportValidity()) return;
            const body = new FormData(form);
            saving = true;
            submit.disabled = true;
            submit.textContent = 'Saving…';
            form.setAttribute('aria-busy', 'true');
            errorBox.hidden = true;
            let saved = false;
            try {
                const response = await fetch(form.action, {
                    method: 'POST', body,
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest',
                        'Accept': 'application/json'
                    }
                });
                if (response.redirected ||
                    !(response.headers.get('content-type') || '')
                        .includes('application/json')) {
                    throw new Error('Could not confirm the save. Refresh and check ' +
                        'the contact list and your sign-in before submitting again.');
                }
                let data;
                try { data = await response.json(); }
                catch {
                    throw new Error('Could not confirm the save. Refresh and check ' +
                        'the contact list before submitting again.');
                }
                if (!response.ok || data.success !== true) {
                    const errors = Array.isArray(data.errors) && data.errors.length
                        ? data.errors : ['Could not save. Check the values and try again.'];
                    throw new Error(errors.join(' '));
                }
                saved = true;
                window.location.reload();
            } catch (error) {
                errorBox.textContent = error instanceof TypeError
                    ? 'The connection was interrupted. Refresh and check the contact ' +
                      'list before submitting again.'
                    : error.message;
                errorBox.hidden = false;
                errorBox.focus();
            } finally {
                if (!saved) {
                    saving = false;
                    submit.disabled = false;
                    submit.textContent = originalText;
                    form.removeAttribute('aria-busy');
                }
            }
        });
    });
})();
```

In `Views/Patients/Details.cshtml`, add `data-contact-modal` to the existing form with `id="addPhoneForm"`, `data-modal-error` to `id="addPhoneError"`, and `data-modal-focus` to the phone-number input. Keep every existing name, ID, field, token and modal attribute.

Replace the current **entire phone-only `@section Scripts` at the bottom** with:

```cshtml
@section Scripts {
    <script src="~/js/contact-modal-forms.js" asp-append-version="true"></script>
}
```

There must be one Scripts section and one submit handler per contact form. Do not keep the old inline phone script as well.

### 1B. Replace the inline address form

In the Contact Information card, replace only the existing `<form asp-action="AddAddress" ...>` through its matching `</form>` with:

```html
<button type="button" class="btn btn-outline-primary"
        data-bs-toggle="modal" data-bs-target="#addAddressModal">
    <i class="bi bi-plus-lg me-1" aria-hidden="true"></i>Add Address
</button>
```

Add the following modal **after the closing div of the existing Add Phone modal, before `@functions`**. It must not be inside any other form:

```cshtml
<div class="modal fade" id="addAddressModal" tabindex="-1"
     aria-labelledby="addAddressTitle" aria-hidden="true">
    <div class="modal-dialog modal-dialog-centered modal-dialog-scrollable">
        <div class="modal-content">
            <form asp-action="AddAddress" method="post" data-contact-modal>
                @Html.AntiForgeryToken()
                <input type="hidden" name="patientId" value="@patient.Id" />
                <div class="modal-header">
                    <h2 class="modal-title fs-5" id="addAddressTitle">Add Address</h2>
                    <button type="button" class="btn-close" data-bs-dismiss="modal"
                            aria-label="Close"></button>
                </div>
                <div class="modal-body">
                    <p class="text-muted">@patient.FullName</p>
                    <div class="alert alert-danger" role="alert" tabindex="-1"
                         data-modal-error hidden></div>
                    <div class="mb-3">
                        <label for="addressStreet" class="form-label">Street</label>
                        <input id="addressStreet" name="Street" class="form-control"
                               maxlength="200" autocomplete="street-address" data-modal-focus />
                    </div>
                    <div class="mb-3">
                        <label for="addressCity" class="form-label">City (required)</label>
                        <input id="addressCity" name="City" class="form-control"
                               maxlength="50" autocomplete="address-level2" required />
                    </div>
                    <div>
                        <label for="addressGovernorate" class="form-label">Governorate</label>
                        <input id="addressGovernorate" name="Governorate" class="form-control"
                               maxlength="50" autocomplete="address-level1" />
                    </div>
                </div>
                <div class="modal-footer">
                    <button type="button" class="btn btn-outline-secondary"
                            data-bs-dismiss="modal">Cancel</button>
                    <button type="submit" class="btn btn-primary">Save Address</button>
                </div>
            </form>
        </div>
    </div>
</div>
```

This retains the old form's three visible fields and existing IsPrimary default. Do not introduce new primary-address semantics in this UI step.

### 1C. Replace the AddAddress controller action

In `Controllers/PatientsController.cs`, replace the AddAddress action **including its two attributes** with:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AddAddress(
    int patientId, CreatePatientAddressRequest model)
{
    var isAjax = Request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest";
    model = new CreatePatientAddressRequest
    {
        City = model.City?.Trim() ?? "",
        Street = model.Street?.Trim(),
        Governorate = model.Governorate?.Trim(),
        PostalCode = model.PostalCode?.Trim(),
        IsPrimary = model.IsPrimary
    };

    if (patientId <= 0)
        ModelState.AddModelError("", "Invalid patient.");
    if (string.IsNullOrWhiteSpace(model.City) || model.City.Length > 50)
        ModelState.AddModelError(nameof(model.City), "Enter a city of up to 50 characters.");
    if (model.Street?.Length > 200)
        ModelState.AddModelError(nameof(model.Street), "Street must be 200 characters or fewer.");
    if (model.Governorate?.Length > 50)
        ModelState.AddModelError(nameof(model.Governorate), "Governorate must be 50 characters or fewer.");
    if (model.PostalCode?.Length > 10)
        ModelState.AddModelError(nameof(model.PostalCode), "Postal code must be 10 characters or fewer.");

    if (!ModelState.IsValid)
    {
        var errors = ModelState.Values.SelectMany(v => v.Errors)
            .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                ? "Check the entered values." : e.ErrorMessage).ToArray();
        if (isAjax) return BadRequest(new { errors });
        Error(string.Join(" ", errors));
        return RedirectToAction(nameof(Details), new { id = patientId });
    }

    var result = await _patientService.AddAddressAsync(patientId, model);
    if (!result.IsSuccess)
    {
        if (isAjax) return BadRequest(new { errors = result.Errors });
        Error(result.Errors.FirstOrDefault() ?? "Could not add the address.");
        return RedirectToAction(nameof(Details), new { id = patientId });
    }

    Success("Address added.");
    if (isAjax) return Json(new { success = true });
    return RedirectToAction(nameof(Details), new { id = patientId });
}
```

**Test before Step 2:** Phone still works; address opens centered; City required; spaces-only City returns a visible error without closing; Street/City/Governorate maximum lengths match 200/50/50; one successful save creates one address; Cancel saves nothing; phone and address popups do not interfere; invalid/session-expired responses do not show success; test at 360px width and with keyboard only.

## Step 2 — contact actions and patient-information readability

**Files:** `Views/Patients/Details.cshtml`, `Controllers/PatientsController.cs`, `wwwroot/css/site.css`.

1. Keep removal as an explicit POST with antiforgery and the current confirmation prompt. A second confirmation-modal implementation is unnecessary for finishing this pass.
2. In the phone removal button, replace the `btn-link ... p-0` classes with `btn btn-sm btn-outline-danger`. Keep its onclick confirmation and form fields. Change its content to `<i class="bi bi-trash me-1" aria-hidden="true"></i>Remove phone`.
3. Apply the same change to the address button with text “Remove address”. Keep each button inside its own existing form. Add `flex-wrap gap-2` to each contact-list row's flex classes so long text does not squeeze the button.
4. In RemovePhone, replace `await _patientService.RemovePhoneAsync(patientId, phoneId);` with:

```csharp
var result = await _patientService.RemovePhoneAsync(patientId, phoneId);
if (!result.IsSuccess)
    Error(result.Errors.FirstOrDefault() ?? "Could not remove the phone.");
else
    Success("Phone removed.");
```

In RemoveAddress use the identical pattern with RemoveAddressAsync(patientId, addressId), “Could not remove the address.” and “Address removed.” Keep both action signatures, attributes and redirects unchanged.

5. In the existing Medical Information card, group the existing dt/dd pairs into two clearly headed blocks: “Patient information” for demographic/source details and “Medical information” for blood group, height, weight, allergies, chronic conditions and notes. Move whole dt/dd pairs; keep the expressions and existing edit links. Use `<h2 class="h6">…</h2>` followed by a `<dl class="row mb-0">` for each block. Do not put a heading directly inside a dl. Do not create editable fields here.
6. For multiline notes/allergies text add `class="clinical-text"` to the containing dd, preserving Razor encoding. Never use Html.Raw on patient-entered text. Use “Not recorded” for missing information; do not imply that an unrecorded allergy was clinically ruled out.
7. Append to site.css:

```css
.clinical-text { white-space: pre-wrap; overflow-wrap: anywhere; }
.contact-value { overflow-wrap: anywhere; min-width: 0; }
```

Add contact-value to the phone/address text wrapper, not the whole removal form.

**Test:** Cancel removal leaves data; confirmed removal removes only the chosen contact; stale/nonexistent contact displays the service failure; long address and multiline notes wrap; no patient data disappears; timeline/report still open a new tab; role-specific shortcuts are unchanged.

## Step 3 — preserve filters through pagination

**Why now:** `_Pagination.cshtml` currently supplies only page and pageSize, so a second page can silently lose the user's search/date/category selection.

Replace the entire `Views/Shared/_Pagination.cshtml` with:

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

This targets the current single-value filters and existing same-controller pagination callers. It does not introduce arbitrary return URLs. Before testing, search the solution for `_Pagination` and include every calling screen in the smoke test. Keep their existing PagedResult<object> mapping and PaginationAction values.

In each filter form, make `method="get"` and the intended `asp-action` explicit. Do not carry a hidden current `page` into a new search; new filters should start on page 1. Keep pageSize if the screen already lets the user choose it.

**Test:** Patients search with enough results for two pages retains search on Next/Previous; Reservations retains DateFrom, DateTo and Category; changing filters starts page 1; no-match search remains no-match; first/last page disabled controls cannot navigate; many pages do not overflow mobile; page URLs remain on the intended controller/action. Test any other shared-partial callers you found.

## Step 4 — consistent page headers, filters and tables

### 4A. Shared CSS

In `wwwroot/css/site.css`, find `.card:hover` and remove just `transform: translateY(-2px);`. Remove its hover-only shadow change too, or remove the complete `.card:hover` rule if those are its only declarations. Ordinary form cards should not move when the user points at an input. Leave normal card styling intact.

Append once:

```css
.page-toolbar { display: flex; flex-wrap: wrap; align-items: center;
    justify-content: space-between; gap: 1rem; margin-bottom: 1.25rem; }
.page-toolbar h1 { margin: 0; font-size: clamp(1.4rem, 2.5vw, 1.85rem);
    overflow-wrap: anywhere; }
.page-actions { display: flex; flex-wrap: wrap; gap: .5rem; }
.page-actions .btn, .filter-panel .btn { min-height: 44px; }
.filter-panel { padding: 1rem; border: 1px solid var(--border-soft);
    border-radius: var(--radius-md); background: var(--surface); }
.form-actions { display: flex; flex-wrap: wrap; gap: .75rem;
    padding-top: 1rem; margin-top: 1rem; border-top: 1px solid var(--border-soft); }
.form-actions .btn { min-height: 44px; }
.empty-state { padding: 2rem 1rem; text-align: center; color: #4b5563; }
.table-responsive:focus-visible, .btn:focus-visible, a:focus-visible {
    outline: 3px solid var(--brand-primary); outline-offset: 3px;
}
@media (max-width: 575.98px) {
    .page-actions { width: 100%; }
    .page-actions > .btn { flex: 1 1 auto; }
}
@media (prefers-reduced-motion: reduce) {
    .card, .btn, .modal.fade .modal-dialog { transition: none !important; }
}
@media print {
    .page-actions, .filter-panel, .form-actions { display: none !important; }
}
```

### 4B. Apply the same header structure, preserving links

Start with `Views/Patients/Index.cshtml`. Move Register Patient into a page-toolbar immediately before the outer card, using this complete header. Remove the old card-header block that duplicated the title/button:

```cshtml
<div class="page-toolbar">
    <div><h1>Patients</h1><p class="text-muted mb-0">Find a patient or register a new patient.</p></div>
    <div class="page-actions">
        <a asp-action="Create" class="btn btn-primary">
            <i class="bi bi-plus-lg me-1" aria-hidden="true"></i>Register Patient
        </a>
    </div>
</div>
```

For `Views/Reservations/Index.cshtml`, use the same structure with h1 “Reservations”. Move its existing Today, New Reservation and Appointment Board anchors into page-actions without altering routes; make New Reservation the primary button. Remove the duplicate old card-header.

Do not refactor into a dynamic header partial yet: these two simple headers can be read and edited directly without hiding authorization or route parameters.

### 4C. Label filters

Patients: add `<label for="patientSearch" class="form-label">Name, phone or national ID</label>` before the search input, and add `id="patientSearch"` to it. Keep `name="search"` and `value="@ViewBag.Search"`. Use `class="row g-3 align-items-end mb-3 filter-panel"` on the form. Add `<a asp-action="Index" class="btn btn-outline-secondary">Clear</a>` beside Search.

Reservations: add IDs `reservationDateFrom`, `reservationDateTo`, `reservationCategory` to the existing DateFrom, DateTo and Category inputs/select. Add matching labels “From”, “To”, “Category”. Keep the exact existing names, values and selected expressions. Use the same filter-panel form classes, normal-size form-control/form-select, and normal-size buttons. Add the same Clear link beside Filter.

For the existing table wrappers on these two pages, add `tabindex="0" role="region" aria-label="Patient results"` or `aria-label="Reservation results"`. Keep table-responsive. Add `scope="col"` to column headers. Replace a blank action header with `<th scope="col"><span class="visually-hidden">Actions</span></th>`. Replace eye-only row-link contents with visible “Open” and preserve all asp-* attributes. Do not make the whole row clickable.

Change no-match text to “No patients match your search.” / “No reservations match these filters.” with a visible Clear filters link to Index. The separate primary create action remains available above.

**Test:** search/date/category still bind; Clear removes filters; pagination still retains filters; at 360/768/1280px actions wrap, table scrolls within its container and body does not scroll horizontally; keyboard focus visible; long names do not overlap actions.

## Step 5 — Reservation Details: clearer actions and truthful collection state

This step includes a small controller/view correction: opening a payment form is a UI action, but eligibility must come from the existing payment service.

### 5A. Load the authoritative collection state

In `Controllers/ReservationsController.cs`, add this field beside the other injected services:

```csharp
private readonly IPaymentService _paymentService;
```

Add `IPaymentService paymentService` as the last constructor parameter (add a comma after IPatientService patientService). In the constructor body add `_paymentService = paymentService;`. The services namespace is already imported and IPaymentService is already used by PaymentsController; no new service implementation is needed.

In Details(int id), immediately after its existing failed-result return and **before `return View(result.Data);`**, insert:

```csharp
ViewBag.CanCollectPayment = false;
ViewBag.CollectionStateKnown = false;
if ((IsAdmin || IsReception) && result.Data is { } reservation)
{
    var due = await _paymentService.GetDueAmountAsync(
        reservation.PatientId, reservation.Id, null);
    ViewBag.CollectionStateKnown = due.IsSuccess;
    ViewBag.CanCollectPayment = due.IsSuccess && due.Data > 0m;
}
```

In `Views/Reservations/Details.cshtml`, replace the complete card whose header text is “Payment” (through its matching closing card div), with:

```cshtml
@if (User.IsInRole("Admin") || User.IsInRole("Reception"))
{
    <div class="card mb-3">
        <div class="card-header">Payment</div>
        <div class="card-body">
            @if (ViewBag.CanCollectPayment == true)
            {
                <a asp-controller="Payments" asp-action="Collect"
                   asp-route-patientId="@Model.PatientId" asp-route-reservationId="@Model.Id"
                   class="btn btn-success w-100">Collect Payment</a>
            }
            else
            {
                <p class="text-muted mb-2">
                    @(ViewBag.CollectionStateKnown == true
                        ? "No collectible balance for this reservation."
                        : "Collection eligibility could not be confirmed. Check the payment summary.")
                </p>
            }
            <a asp-controller="Payments" asp-action="PatientSummary"
               asp-route-patientId="@Model.PatientId"
               class="btn btn-outline-secondary w-100 mt-2">Payment Summary</a>
        </div>
    </div>
}
```

Do not replace a failed due lookup with “Fully paid”. Keep Collect GET and POST validation; the amount can change after this page was rendered.

### 5B. Correct the completion redirect for roles

In ReservationsController.UpdateStatus replace only the existing `if (statusId == 3 && result.Data is { IsPaid: false } r) { ... }` block with:

```csharp
if (result.Data is { StatusName: "Completed" } r && (IsAdmin || IsReception))
{
    var due = await _paymentService.GetDueAmountAsync(r.PatientId, id, null);
    if (due.IsSuccess && due.Data > 0m)
        return RedirectToAction("Collect", "Payments",
            new { patientId = r.PatientId, reservationId = id });
    if (!due.IsSuccess)
        Error("Reservation completed, but collection eligibility could not be confirmed. Check the payment summary.");
}
```

Keep the existing final redirect to Details. Keep the call to UpdateStatusAsync, its current user ID and its failure branch. This does not change the atomic transition or authorize another role to complete: it only avoids sending a doctor to a payment page they cannot use.

### 5C. Layout and status explanations

In the view's top header, use page-toolbar/page-actions and h1 as in Step 4. Preserve Edit's completed-state disabling and the Cancel POST. Keep cancellation visually secondary/danger; do not make it the primary action.

Add the following paragraph below the queue badge/timestamps:

```html
<p class="small text-muted mt-2 mb-0">
    Check in places the patient in Waiting. Start Consultation begins the visit.
    Completing an active consultation finishes the queue entry.
</p>
```

Above the status form declare:

```cshtml
@{
    var canCompleteFromQueue = Model.StatusName == "Confirmed"
        && Model.QueueStatus == PatientQueueStatus.InConsultation
        && Model.ConsultationStartedAtUtc.HasValue;
}
```

The enum namespace is already imported. On the existing Completed option add `disabled="@(!canCompleteFromQueue)"`; retain value and selected expression. Keep the existing completed-state disabling on select/button. Add visible help under the form: “Completion is available after consultation starts. The server rechecks the current queue state.” This is a UI hint, not a replacement for server validation. Do not rewrite other transitions here.

**Test:** Pending/Waiting have no collectible link; InConsultation with positive balance has one; Finished with positive balance still has one; zero/fully covered balance does not; reconciliation failure is not described as paid; doctor sees no payment collection card; successful completion as doctor stays on an authorized page; reception/admin follows collection only when due is positive; stale-tab/forged completion from Waiting is rejected by existing server logic; Completed Edit stays disabled.

## Step 6 — doctor queue and appointment-board clarity

**Files:** `Views/Reservations/Queue.cshtml`, `Board.cshtml`, `_BoardAppointment.cshtml`.

1. In Queue replace the top header wrapper classes with page-toolbar, change h4 to h1, and keep the conditional “Doctor Queues” / “My Queue” title and current date.
2. Keep clinic and doctor filters, their element IDs `queueClinic`/`queueDoctor`, their model bindings and existing doctor-option script. Do not remove the doctor filter because multiple doctors can share one clinic.
3. Change the Apply / Refresh button text to “Apply filters / Refresh”. Add `filter-panel` to its existing form classes. Add this visible text below the form: “Queue data updates when you refresh. Actions are checked against the latest saved state.” Do not introduce timed refresh that might discard input or move keyboard focus.
4. Retain separate Waiting and In Consultation counts. Below them add a small sentence: “These counts reflect the selected filters.” Do not replace them with global dashboard counts.
5. In the queue table, preserve all existing eligibility conditions and POST forms. Make action text visible (“Start Consultation”, “Open Consultation”, “Open reservation”), add scope to headers and an accessible, focusable table-responsive wrapper as in Step 4. Do not merge several patients into one form.
6. Give its no-result block the empty-state class. Use “No patients in this queue for the selected filters.” Keep Reset accessible. For doctors, do not add an All Doctors option outside the existing Model.CanViewAllQueues block.
7. In Board apply page-toolbar/h1/page-actions to its existing header without changing links or filters. Keep columns grouped by reservation status. Add below the header: “Columns show reservation status. Use Doctor Queues to follow Waiting and In Consultation.” This avoids confusing Confirmed with consultation progress.
8. In `_BoardAppointment.cshtml`, preserve existing patient/reservation links and status badges. Give icon-only actions readable text or an aria-label. Add `overflow-wrap: anywhere` via a class to long patient names; do not truncate away identity with no way to inspect it.

**Test:** doctor A cannot see doctor B by changing query parameters; reception can filter clinic then doctor; selected filters survive refresh; waiting/start buttons behave exactly as before; a stale tab cannot start a cancelled visit; board counts/status columns unchanged; mobile cards/table remain usable; no automatic refresh or duplicate action request.

## Step 7 — consultation workspace and long-form usability

**Files:** `Views/MedicalRecords/Details.cshtml`, `Create.cshtml`, `Edit.cshtml`, `PatientRecords.cshtml`.

This is layout-only. Do not rewrite clinical POST actions, introduce automatic saving, or alter the record lock/completion checks.

1. In Details change the top header to page-toolbar and h1 “Medical record #@Model.Id”. Keep patient name and record date directly below it. Keep the locked/unlocked badge as readable text.
2. Move the existing Back to Queue, reservation link, completion form, Edit link and admin Lock form into page-actions. **Move complete form elements**, including tokens and hidden inputs. Keep the existing `ViewBag.CanCompleteConsultation`, IsLocked and role checks unchanged. Never wrap these in another form.
3. Add a patient workspace link with `asp-controller="Patients" asp-action="Details" asp-route-id="@Model.PatientId"`. Its text should be “Patient workspace”. Keep Back to Queue too.
4. Under the header add an anchor navigation bar using existing workspace-shortcuts styling. Link only to sections that exist on the rendered page: Clinical notes, Treatments, Prescriptions, Analyses. Assign IDs `record-clinical`, `record-treatments`, `record-prescriptions`, `record-analyses` to the corresponding existing outer section containers. If a section is role/condition gated, put its navigation link under the same condition. Do not rename existing IDs used by scripts.
5. Use h2 with class h5 for section headings and clinical-text on displayed multiline note values. Keep normal Razor output. Keep existing add/edit/delete forms, modal targets, prescription links and analysis scripts in their original sections.
6. On Create/Edit, retain the existing single clinical form. Group existing fields with `<fieldset class="mb-4">` and `<legend class="h5">…</legend>` around the current logical field groups. Do not cross form boundaries. Every asp-for remains unchanged. Use normal-size labels/inputs and rows="4" or more for clinical narrative textareas.
7. Wrap the existing final Save and Cancel controls in `<div class="form-actions">`. Keep their original endpoints and IDs. Keep validation summary and field-validation elements inside the form. Place the summary before the first editable group.
8. Use page-toolbar/h1 and responsive-table conventions on PatientRecords; retain links opening records and existing patient ID routes. Do not duplicate the full patient record on Patient Details.

**Test:** doctor resumes an active consultation through the existing queue; existing record is reused; adding prescriptions/analyses still works; failed validation retains every field; completion remains conditional; locked records cannot be edited by manipulating URLs; long notes retain line breaks; section links target the correct section; patient workspace/back links return correctly; printing prescriptions unaffected.

## Step 8 — financial screens and collection-form clarity

**Files:** `Views/Payments/PatientSummary.cshtml`, `Collect.cshtml`, `Dashboard.cshtml`, `Report.cshtml`. Keep Audit popup and its JavaScript unchanged except accessible labels if needed.

### 8A. Patient summary

Replace the initial patient-name header block with:

```cshtml
<div class="page-toolbar">
    <div>
        <h1>@Model.PatientName</h1>
        <p class="text-muted mb-0">Payment summary</p>
    </div>
    <div class="page-actions">
        <a asp-controller="Patients" asp-action="Details"
           asp-route-id="@Model.PatientId" class="btn btn-outline-secondary">Patient workspace</a>
        <a asp-action="Dashboard" class="btn btn-outline-secondary">Payment dashboard</a>
    </div>
</div>
```

Keep all four amount expressions and the UnallocatedCoverage branches. Change each summary column from `col-md-3` to `col-12 col-sm-6 col-xl-3` for readable tablet cards. Change the “Pending Payments” heading to “Ready for collection”, followed inside its body by `<p class="small text-muted">Balances currently eligible for collection.</p>`. Keep the existing list and exact per-row reservation/enrollment IDs.

The Payment History table currently lacks its own responsive wrapper. Wrap only that table (from its opening table to matching closing table) in `<div class="table-responsive" tabindex="0" role="region" aria-label="Payment history">…</div>`. Do not include modal markup in this wrapper. Add scope to headers and a readable Actions header as in Step 4. Preserve Audit triggers, data attributes and script IDs.

### 8B. Collection form

Keep collection as a full page: amount, clinic, method and validation deserve room. Add a page-toolbar/h1 “Collect Payment” above its card and a secondary Patient Summary link using Model.PatientId. Replace the card's inline max-width style with `class="card payment-form-card"`; append `.payment-form-card { max-width: 46rem; }` to site.css.

Inside the card, before the form, add this context block using fields already on CollectPaymentRequest:

```cshtml
<div class="alert alert-light border">
    <strong>Patient #@Model.PatientId</strong>
    @if (Model.ReservationId.HasValue)
    {
        <span> · Reservation #@Model.ReservationId</span>
    }
    @if (Model.EnrollmentId.HasValue)
    {
        <span> · Nutrition enrollment #@Model.EnrollmentId</span>
    }
</div>
```

This provides dependable context even when POST validation redisplays the model; do not invent a ViewBag.PatientName that the controller does not populate on every path.

Keep the ClinicId select and its selected model value. Do not disable it without an equivalent submitted value. The existing GET preselects the reservation clinic and the service validates the transaction; preserve both. Replace the amount helper text with: “Enter the amount being collected. For partial payment, enter less than the current balance. The balance is checked again when you submit.” Keep the amount binding/step, hidden IDs and antiforgery token unchanged.

Add `asp-validation-for` spans below ClinicId, Amount, PaymentMethod, ReferenceNumber and Notes if absent, each with class text-danger. Keep the existing validation summary and `_ValidationScriptsPartial`. Wrap the existing submit/Cancel controls in form-actions. Do not attach the contact-modal helper, automatic retries or client-calculated deductions.

### 8C. Dashboard/report

Use page-toolbar/h1 for the existing titles and filter-panel for existing GET date filters. Keep date names, values and report drill-down route arguments. Add labels where absent, responsive wrappers to wide tables, scope to headers, and explicit no-results text. Keep all calculated numbers, permission checks and audit actions unchanged.

**Test:** no collectible balance has no active collection action; partial balance is exact; finished unpaid remains collectible; reservation clinic is preselected and remains correct after invalid submit; zero/negative/excess/stale amounts follow existing validation; successful payment appears once; cancellation/write-off behavior unchanged; unallocated coverage still blocks collection; audit opens styled in a popup from summary/report; date filters and dashboard drill-down agree. Do not delete data to make this test pass.

## Step 9 — follow-ups and task-focused dashboard navigation

**Files:** `Views/FollowUps/Index.cshtml`, `Views/Dashboard/Index.cshtml`.

Follow-ups: convert its initial h4/header to page-toolbar/h1 while retaining Model.CanViewAll title logic. Keep the sentence explaining that results are pending, unbooked and not dismissed. Add filter-panel to the current GET form; preserve Due/From/To/DoctorId binding, validation and role checks. Apply responsive-table and visible-action-label patterns. Keep Book/Dismiss controls with their existing conditions and forms. Use “No pending follow-ups match these filters.” for empty results. Do not add new status tabs, bulk dismiss or a new scheduling flow.

Dashboard: insert the following block immediately after the initial Razor block and before the first summary-card row:

```cshtml
<div class="page-toolbar">
    <div><h1>Dashboard</h1><p class="text-muted mb-0">Choose your next task.</p></div>
</div>
<nav class="workspace-shortcuts" aria-label="Dashboard shortcuts">
    @if (User.IsInRole("Admin") || User.IsInRole("Reception") || User.IsInRole("Doctor"))
    {
        <a asp-controller="Reservations" asp-action="Queue" class="btn btn-outline-primary">
            @(User.IsInRole("Doctor") && !User.IsInRole("Admin")
                && !User.IsInRole("Reception") ? "My Queue" : "Doctor Queues")
        </a>
    }
    @if (User.IsInRole("Admin") || User.IsInRole("Reception"))
    {
        <a asp-controller="Patients" asp-action="Index" class="btn btn-outline-primary">Find Patient</a>
        <a asp-controller="Reservations" asp-action="Create" class="btn btn-primary">Book Reservation</a>
        <a asp-controller="Payments" asp-action="Dashboard" class="btn btn-outline-primary">Payments</a>
    }
</nav>
```

Keep every existing dashboard metric and its server query unchanged. The shortcuts are additive; existing navigation for other roles stays available. Do not relabel the unpaid count as all Pending/Confirmed reservations.

**Test:** reception can start patient search/booking/payment tasks; doctors get own queue navigation; nutritionist gets no doctor-queue shortcut; follow-up filtering still works; booked follow-up leaves pending results; cancellation reopens according to existing rules; dismiss validation remains; dashboard money/counts unchanged.

## Step 10 — remaining forms, navigation and accessibility finish

Finish the existing screens without adding new functionality. Apply the already-tested patterns rather than introducing another component system.

### 10A. Form pass

Inspect existing views under Patients (Create/EditBasic/EditMedical), Reservations (Create/Edit), Nutrition and Analysis. For each rendered form:

1. Keep one h1 page title; use existing page-toolbar styles for title/back action.
2. Keep an explicit Cancel/Back anchor to the existing owning patient/list screen. Preserve its patient/reservation/enrollment route ID. Do not implement `history.back()` as the only way out.
3. Add visible labels with matching for/id, or asp-for labels. Placeholders are hints, not labels.
4. Retain validation summary and field messages; retain posted names/IDs, hidden context, antiforgery and role checks.
5. Use row g-3 and col-12 col-md-6 for short paired fields; full-width for long text. Keep a single containing form and avoid moving controls outside it.
6. Use form-actions for Save/Cancel. Do not rename action parameters or replace native form submission with fetch.
7. Add table-responsive wrappers to wide nested tables. Preserve script-used IDs and data-* attributes.

These are mechanical presentation changes. If a screen needs a new DTO, service query or workflow to fit the design, leave that change out and request review; that is beyond this finishing pass.

### 10B. Shared layout

In `Views/Shared/_Layout.cshtml`, add `aria-label="Close"` to alert close buttons that lack it. Keep errors and warnings persistent; only success may auto-dismiss as currently implemented.

Inside the sidebar's ul, change any direct `<div class="nav-section-label ...">…</div>` child to `<li class="nav-section-label ..." role="presentation">…</li>`. Change both opening and closing tag, preserving class/content. Do not change divs outside the list. Keep offcanvas controls, sidebar ID, skip link and mainContent target.

Do not add a second Bootstrap bundle, site.css, or site.js include. Keep versioned local assets. Do not change the font/library versions during this pass.

### 10C. Keyboard, responsive and print checks

Walk through each principal screen using Tab/Shift+Tab/Enter/Escape. Every action needs an accessible name; decorative icons can use aria-hidden. Every modal needs a visible title, labelledby, labelled Close control, sensible initial focus and focus returning to its trigger. Keep the contact helper's blocked dismissal while saving.

Check 360px, 768px and 1280px widths, then browser zoom at 200%. Content may grow vertically; it must not overlap or disappear. Tables may scroll in their own labelled region. Check long Arabic and English names, long addresses, empty values and large balances using existing demo records; do not change application-wide language/direction or currency behavior in this pass.

Print an existing prescription and reservation-history report: no clipped clinical content, no sidebar, and no UI action buttons covering output. Do not redesign the report's data content as part of the shell styling.

**Test:** all relevant forms still save and retain invalid values; keyboard operation works without a mouse; no new browser-console error; mobile sidebar closes and focus remains usable; persistent warning/error messages stay visible; existing print outputs remain readable.

## Step 11 — final acceptance and handoff for review

Run the complete journey with a fresh demo reservation and separate browser sessions for reception and its assigned doctor. Record IDs so failures can be reproduced.

| Test | Expected result |
| --- | --- |
| Reception opens patient workspace | Identity, contact details and permitted shortcuts clear |
| Add phone/address; invalid then valid | Error stays in popup; values retained; successful item visible once |
| Book from patient workspace | Correct patient preselected |
| New Pending reservation | Not collectible |
| Check In | Confirmed + Waiting; not collectible |
| Complete while Waiting | UI discourages; server rejects a forged/stale submission |
| Assigned doctor starts consultation | Confirmed + InConsultation |
| Other doctor attempts access | Existing ownership restriction enforced |
| Reception collects partial amount | Correct reservation/clinic; positive remaining balance retained |
| Doctor saves notes/prescription/analysis | Same linked record; no lost fields |
| Complete active consultation | Completed + Finished; no unrelated payment created by the UI |
| Finished reservation with remaining balance | Still collectible; collection link disappears after full coverage |
| Edit completed reservation | Disabled and server-protected |
| Cancel a separate waiting reservation | Existing cancellation/Left rules preserved |
| Search/filter, then next page | Search/filter still active |
| Payment audit | Popup, styled, readable, pagination functional |
| Timeline/history | New tab, correct patient |
| Follow-up booking/cancellation | Existing pending/booked/reopen behavior preserved |
| Roles | Reception, Doctor, Admin, Nutritionist see only their permitted actions |
| Responsive/keyboard/print | No overlap, inaccessible control or clipped print content |

For concurrency, use two tabs to attempt the same queue transition and to collect against the same balance. The UI must show the server's rejection/recomputed state; do not weaken existing transaction checks to make both submissions succeed.

### Completion criteria

The UI/UX pass is finished when Steps 1–10 and this matrix pass, builds succeed, there are no new console errors, and the existing business rules still hold. Stop here and request a combined review before starting another feature batch. No additional feature is required to call this phase complete.

### Implementation log

| Step | Build | Tests | Checkpoint / notes |
| --- | --- | --- | --- |
| 1 Contact popups | | | |
| 2 Patient readability/removal feedback | | | |
| 3 Pagination | | | |
| 4 Lists/shared presentation | | | |
| 5 Reservation actions | | | |
| 6 Queue/board | | | |
| 7 Consultation | | | |
| 8 Payments | | | |
| 9 Follow-ups/dashboard | | | |
| 10 Forms/accessibility | | | |
| 11 End-to-end acceptance | | | |

### What to send when requesting help

Include the step/substep number, affected file, exact build error or browser message, signed-in role, starting reservation/queue/payment state, expected vs actual result, and the relevant changed block. For UI problems, a screenshot is useful. For final review, provide the changed files/diff and completed test log. Do not send database credentials or real patient-identifying data.

### Recovering from a failed step

Keep the last passing checkpoint. Revert only the changes from the failing step using your source-control tool, keeping unrelated work. Rebuild and retest the previous baseline. Do not reset the database or run payment maintenance/backfill actions to fix a presentation error. If the source has evolved since this guide was prepared, ask for review before adapting a controller replacement.
