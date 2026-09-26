# Next step — complete the reservation booking/editing workflow UI

Prepared 24 September 2026 for `D:\MyDevWork\clinic-MS\SafyaClinic`.

This is the next **UI/UX step**, not a new feature. Reservation Create/Edit are still in their earlier form layout. Finish them after the fixes in `UIUX-Fixing-Guide-2026-09-24.md` pass. The outcome is a clear patient → service → appointment → save flow, with preserved validation and a predictable return path.

No schema change, package installation or service rewrite is required. Keep the existing confirmed/waiting/in-consultation/completed/finished rules, payment eligibility, ownership checks and server transactions. These instructions have been checked against current source but have not been compiled or browser-tested.

## N1 — prepare and define acceptance

1. Complete F1–F10 from the fixing guide. In particular, do not style the booking form while its patient ID can remain selected after changing the visible name.
2. Create a source-control checkpoint with the passing fixes.
3. Open Reservations/Create in two ways: from the main list and from a patient's Book Reservation action. Record current behavior of patient preselection, treatment amount/duration autofill, failed submission, and success redirect.
4. Open Edit for a noncompleted reservation; confirm existing doctor/clinic/service/date/time/amount values. Completed must remain rejected by the controller/service.
5. Work only on the files named below. Keep controller POST actions unchanged in this step.

Files: `Views/Reservations/Create.cshtml`, `Views/Reservations/Edit.cshtml`, `Views/Reservations/Details.cshtml`, `wwwroot/css/site.css`, relative to `SafyaClinic.Web`.

**Acceptance:** one visible page title, clear form groups, labelled fields, visible validation, correct selected patient, no lost values after rejection, role-appropriate next action, and no mobile overlap.

## N2 — add the small shared form styles

Append once to `wwwroot/css/site.css`:

```css
.reservation-form-card { max-width: 60rem; }
.reservation-fieldset { min-width: 0; margin: 0 0 1.5rem; padding: 0; border: 0; }
.reservation-fieldset > legend { float: none; width: auto; margin-bottom: .75rem;
    font-size: 1.1rem; font-weight: 600; color: var(--brand-ink); }
.reservation-form-help { max-width: 70ch; color: #4b5563; }
```

Reuse existing page-toolbar, page-actions and form-actions. Do not add fixed heights or sticky action bars: they can cover validation and mobile keyboards. Hard-refresh and verify the existing patient/payment layouts did not change.

## N3 — replace the Create form's presentation

File: `Views/Reservations/Create.cshtml`.

Keep the existing @model, @using directives and initial Razor block defining doctors, clinics and treatmentTypes. Keep the **complete Scripts section from F1**, including the new patient-picker include and treatment autofill listener.

Replace the markup starting at the first `<div class="card" style="max-width:700px;">` through that card's final closing div **immediately before `@section Scripts`**, with the following entire block. Do not replace the Scripts section, and do not leave the original form beneath the new form.

```cshtml
<div class="page-toolbar">
    <div>
        <h1>Book reservation</h1>
        <p class="reservation-form-help mb-0">Select the patient, service and appointment time.</p>
    </div>
    <div class="page-actions">
        @if (Model.PatientId > 0)
        {
            <a asp-controller="Patients" asp-action="Details" asp-route-id="@Model.PatientId"
               class="btn btn-outline-secondary">Patient workspace</a>
        }
        <a asp-action="Index" class="btn btn-outline-secondary">Reservations</a>
    </div>
</div>
<div class="card reservation-form-card">
    <div class="card-body">
        <form asp-action="Create" method="post" id="reservationCreateForm">
            @Html.AntiForgeryToken()
            <div asp-validation-summary="All" class="alert alert-danger py-2"
                 role="alert" tabindex="-1"></div>

            <fieldset class="reservation-fieldset">
                <legend>1. Patient</legend>
                <div class="row g-3 align-items-end">
                    <div class="col-12 col-lg-8">
                        <label for="patientSearchInput" class="form-label">Patient (required)</label>
                        <input type="hidden" asp-for="PatientId" id="patientIdHidden" />
                        <div class="position-relative">
                            <input type="text" id="patientSearchInput" class="form-control"
                                   placeholder="Search by name or mobile number"
                                   value="@(ViewBag.SelectedPatientName ?? "")"
                                   autocomplete="off" required
                                   data-search-url="@Url.Content("~/api/patients/search")"
                                   aria-describedby="patientSearchHelp patientSearchStatus" />
                        </div>
                        <small id="patientSearchHelp" class="text-muted">Type at least 2 characters, then select a patient.</small>
                        <div id="patientSearchStatus" class="small mt-1" role="status" aria-live="polite"></div>
                        <span asp-validation-for="PatientId" class="text-danger"></span>
                    </div>
                    <div class="col-12 col-lg-4">
                        <button type="button" id="registerPatientButton"
                                data-popup-url="@Url.Action("Create", "Patients", new { popup = true })"
                                class="btn btn-outline-primary">
                            <i class="bi bi-person-plus me-1" aria-hidden="true"></i>Register new patient
                        </button>
                        <div class="small text-muted mt-1">Opens a separate registration window.</div>
                    </div>
                </div>
            </fieldset>

            <fieldset class="reservation-fieldset">
                <legend>2. Clinic and service</legend>
                <div class="row g-3">
                    <div class="col-12 col-md-6">
                        <label asp-for="ClinicId" class="form-label">Clinic (required)</label>
                        <select asp-for="ClinicId" class="form-select" required>
                            <option value="">— Select clinic —</option>
                            @foreach (var c in clinics) { <option value="@c.Id">@c.Name</option> }
                        </select>
                        <span asp-validation-for="ClinicId" class="text-danger"></span>
                    </div>
                    <div class="col-12 col-md-6">
                        <label asp-for="DoctorId" class="form-label">Doctor (required)</label>
                        <select asp-for="DoctorId" class="form-select" required>
                            <option value="">— Select doctor —</option>
                            @foreach (var d in doctors)
                            {
                                <option value="@d.Id">@d.FullName @(d.Specialization != null ? $"({d.Specialization})" : "")</option>
                            }
                        </select>
                        <span asp-validation-for="DoctorId" class="text-danger"></span>
                    </div>
                    <div class="col-12 col-md-6">
                        <label for="treatmentTypeSelect" class="form-label">Treatment type (required)</label>
                        <select asp-for="TreatmentTypeId" id="treatmentTypeSelect" class="form-select" required>
                            <option value="">— Select treatment type —</option>
                            @foreach (var t in treatmentTypes)
                            {
                                <option value="@t.Id"
                                        data-cost="@(t.DefaultCost?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "")"
                                        data-duration="@t.DurationMinutes">
                                    @t.TypeName @(t.DefaultCost != null ? $"— {t.DefaultCost:C}" : "")
                                </option>
                            }
                        </select>
                        <span asp-validation-for="TreatmentTypeId" class="text-danger"></span>
                    </div>
                    <div class="col-12 col-md-6">
                        <label asp-for="Category" class="form-label">Category</label>
                        <select asp-for="Category" class="form-select">
                            <option value="InternalMedicine">Internal Medicine</option>
                            <option value="Nutritional">Nutritional</option>
                        </select>
                        <span asp-validation-for="Category" class="text-danger"></span>
                    </div>
                </div>
            </fieldset>

            <fieldset class="reservation-fieldset">
                <legend>3. Appointment and amount</legend>
                <div class="row g-3">
                    <div class="col-12 col-md-6">
                        <label asp-for="ReservationDate" class="form-label">Date (required)</label>
                        <input asp-for="ReservationDate" type="date" class="form-control" required />
                        <span asp-validation-for="ReservationDate" class="text-danger"></span>
                    </div>
                    <div class="col-12 col-md-6">
                        <label asp-for="ReservationTime" class="form-label">Time (required)</label>
                        <input asp-for="ReservationTime" type="time" class="form-control" required />
                        <span asp-validation-for="ReservationTime" class="text-danger"></span>
                    </div>
                    <div class="col-12 col-md-6">
                        <label for="durationInput" class="form-label">Duration (minutes)</label>
                        <input asp-for="DurationMinutes" id="durationInput" type="number" class="form-control" />
                        <span asp-validation-for="DurationMinutes" class="text-danger"></span>
                    </div>
                    <div class="col-12 col-md-6">
                        <label for="totalAmountInput" class="form-label">Reservation amount</label>
                        <input asp-for="TotalAmount" id="totalAmountInput" type="number" step="0.01" class="form-control"
                               aria-describedby="reservationAmountHelp" />
                        <span asp-validation-for="TotalAmount" class="text-danger"></span>
                    </div>
                </div>
                <p id="reservationAmountHelp" class="small text-muted mt-2 mb-0">
                    Selecting a treatment fills its configured amount and duration when available.
                    Review both before saving. Booking does not collect payment.
                </p>
            </fieldset>

            <fieldset class="reservation-fieldset">
                <legend>4. Additional information</legend>
                <div class="mb-3">
                    <label asp-for="Reason" class="form-label">Reason</label>
                    <input asp-for="Reason" class="form-control" />
                    <span asp-validation-for="Reason" class="text-danger"></span>
                </div>
                <div>
                    <label asp-for="Notes" class="form-label">Notes</label>
                    <textarea asp-for="Notes" class="form-control" rows="4"></textarea>
                    <span asp-validation-for="Notes" class="text-danger"></span>
                </div>
            </fieldset>
            <div class="form-actions">
                <button type="submit" class="btn btn-primary">Book reservation</button>
                <a asp-action="Index" class="btn btn-outline-secondary">Cancel</a>
            </div>
        </form>
    </div>
</div>
```

The displayed patient workspace link reflects the server-loaded patient. It is navigation, not a live client selection summary. The status text next to the picker is the live selected-patient indicator. No clinic-dependent doctor filtering is introduced here; preserve the current service validation.

**Build and test now:** exactly one form and one patient-picker handler; patient preselection; new patient popup; typing after selecting clears hidden ID; all three custom-ID labels focus their controls; treatment amount/duration updates on change; invalid POST retains values; successful booking opens Details. Check browser console. Do not proceed if this fails.

## N4 — make Edit match without changing its request contract

File: `Views/Reservations/Edit.cshtml`.

1. Keep @model UpdateReservationRequest and the existing doctors/clinics/treatmentTypes block.
2. Immediately after that block, add:

```cshtml
<div class="page-toolbar">
    <div>
        <h1>Edit reservation #@ViewContext.RouteData.Values["id"]</h1>
        <p class="reservation-form-help mb-0">Update the appointment details. Change status on the reservation details page.</p>
    </div>
    <div class="page-actions">
        <a asp-action="Details" asp-route-id="@ViewContext.RouteData.Values["id"]"
           class="btn btn-outline-secondary">Reservation details</a>
    </div>
</div>
```

3. Change the outer `<div class="card" style="max-width:700px;">` to `<div class="card reservation-form-card">`. Delete only its redundant card-header line. Keep card-body and the existing form method=post.
4. Change its single summary from ModelOnly to All; add role=alert and tabindex=-1 as in Create.
5. Inside the form, regroup the existing fields by moving complete col-md-6/col-12 blocks. Replace the one outer `row g-3` wrapper with three fieldsets using the following structure. **This is a structural map, not paste-ready markup with omitted fields**:

| Fieldset legend | Existing blocks to put in a `div class="row g-3"` inside it |
| --- | --- |
| Clinic and service | ClinicId, DoctorId, TreatmentTypeId |
| Appointment and amount | ReservationDate, ReservationTime, DurationMinutes, TotalAmount |
| Additional information | Reason, Notes |

For each fieldset use `<fieldset class="reservation-fieldset"><legend>Exact legend above</legend><div class="row g-3">` then the **complete existing field blocks**, then `</div></fieldset>`. Each moved block keeps its loop, binding, selected expression, data-cost, data-duration and ID. Change short field column classes to col-12 col-md-6. Notes stays col-12 and becomes rows=4.

6. Move the informational alert about changing status from Details outside the row wrappers, just after the summary. Keep it inside the form. Remove its now-unneeded col-12 wrapper. Do not add a status input to Edit.
7. Replace the three labels that use custom input IDs with explicit for labels: treatmentTypeSelect, durationInput, totalAmountInput. Use the same text as Create. Keep other asp-for labels.
8. Add an `asp-validation-for` span with text-danger after each select/input/textarea. Field list: DoctorId, ClinicId, TreatmentTypeId, ReservationDate, ReservationTime, DurationMinutes, TotalAmount, Reason, Notes. These property names already exist; do not introduce PatientId or Category on UpdateReservationRequest.
9. Change the final `div class="mt-3"` to `div class="form-actions"`. Keep Save Changes and the existing Cancel Details route.
10. Keep the Scripts section and treatment change listener unchanged. Do not trigger a synthetic change on initial page load: it would overwrite an existing custom amount/duration with treatment defaults.

**Build and test:** opening Edit retains saved custom amount/duration; changing treatment updates defaults as before; service rejection keeps entered fields and option lists; status still edited only from Details; completed reservation Edit rejected; Cancel does not POST; inspect that each field appears exactly once inside the single form.

## N5 — give the reservation a correct clinical next action

The current Clinical card always offers Create Medical Record, even when a linked consultation should be resumed. The existing `MedicalRecords/Consultation(reservationId)` endpoint already resolves the consultation context. Use it for active consultation, preserving its ownership checks.

File: `Views/Reservations/Details.cshtml`. F5 must already be applied.

Within the existing Doctor/Admin conditional, replace the entire Clinical card only with:

```cshtml
<div class="card">
    <div class="card-header">Clinical</div>
    <div class="card-body">
        @if (hasUserId &&
            (User.IsInRole("Admin") || Model.DoctorId == currentUserId) &&
            Model.StatusName == "Confirmed" &&
            Model.QueueStatus == PatientQueueStatus.InConsultation)
        {
            <a asp-controller="MedicalRecords" asp-action="Consultation"
               asp-route-reservationId="@Model.Id"
               class="btn btn-primary w-100">Open consultation</a>
        }
        else
        {
            <p class="small text-muted">Use the queue actions to start an eligible consultation.
                Existing records are available in the patient history.</p>
        }
        <a asp-controller="MedicalRecords" asp-action="PatientRecords"
           asp-route-patientId="@Model.PatientId"
           class="btn btn-outline-secondary w-100 mt-2">Patient medical records</a>
    </div>
</div>
```

`hasUserId` and `currentUserId` already exist at the top of this view. Keep its using for PatientQueueStatus. Keep the existing Check In and Start Consultation POST forms, their conditions, tokens and controller calls. Do not implement consultation creation in JavaScript.

This changes the reservation-specific entry point. It does not remove standalone New Record from the patient-record screen. Keep server-side permission checks; hiding a link does not grant or revoke authorization.

**Test:** assigned doctor in InConsultation opens the current consultation; existing record is reused; Waiting shows Start Consultation only through existing eligible action; completed reservation offers medical-record history; other doctor cannot resume by manipulating the URL; reception sees no clinical card; no automatic completion/payment occurs.

## N6 — verify errors without changing financial or scheduling rules

Use demo data and test these rejected submissions independently. Correct the fields and retry after each rejection:

| Scenario | Expected UI |
| --- | --- |
| Typed name without selecting patient | Picker explains selection requirement; no booking |
| Invalid/missing doctor, clinic or treatment | Required/field/summary feedback; no lost patient selection |
| A server-rejected appointment | Summary shows existing service message; all fields repopulated |
| Invalid numeric/date input | Browser or server message identifies the affected field |
| Expired login while searching | Search unavailable/sign-in message, not “No patients” |
| Existing custom amount on Edit | Retained until user intentionally changes treatment/amount |
| Completed reservation Edit request | Existing server protection, no mutation |

Do not weaken validation to make a test pass. Do not introduce date/time conversions; preserve the application's current reservation time convention. Do not label the amount as currently payable: collectible eligibility still depends on queue state and existing balance rules.

## N7 — responsive, keyboard and role acceptance

At 360px, 768px and 1280px, test Create and Edit. Also test 200% zoom. Fieldsets should stack naturally, dropdown results stay within the form width and action buttons remain visible. Use Tab, Shift+Tab, arrows, Enter and Escape to select a patient and submit. Click every label. Test a long patient name and Arabic name.

Run as Reception, assigned Doctor and Admin. Use Nutritionist only on existing authorized routes; do not add queue/payment access to make navigation look consistent. Verify URLs opened directly still obey existing policies.

Run a short end-to-end journey: Book → Check In (Confirmed/Waiting) → Start Consultation (Confirmed/InConsultation) → record → complete (Completed/Finished) → permitted collection for remaining balance. Also verify partial payment remains collectible after finishing. These transitions are regressions to test, not logic to reimplement.

## N8 — checkpoint and request review

| Step | Build | Tests / notes |
| --- | --- | --- |
| N1 Baseline | | |
| N2 Styles | | |
| N3 Create | | |
| N4 Edit | | |
| N5 Clinical entry | | |
| N6 Error paths | | |
| N7 End-to-end/roles/mobile | | |

When all pass, save a checkpoint and request review. Send the changed files or diff, test log, and any remaining browser/build error. If a step fails, send its number, exact error, role and starting reservation/queue state. Do not include real patient data or connection strings.

Stop after this bounded step. A new feature batch is a separate decision; completing the reservation workflow and review is the next milestone.
