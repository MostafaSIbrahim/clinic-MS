# Payment access changes

Implemented rules:

| Role | Payment dashboard | Payment report |
| --- | --- | --- |
| Admin | Full dashboard and financial drill-downs | All transactions within selected dates |
| Reception / ReceptionAdmin | Unpaid InConsultation and Finished balances only | Today only |
| Doctor | No dashboard access | Transactions linked to reservations or nutrition enrollments assigned to the signed-in doctor, within selected dates |

Role names are exact. The reception admin role is assumed to be `ReceptionAdmin`. Doctor ownership means the assigned doctor, not the payment collector. Admin takes precedence when a user has multiple roles; otherwise Doctor permits the doctor-scoped date-range report. Other roles have no access to these payment routes.

Reports remain empty until Filter is clicked. Reception's URL dates cannot widen the today-only range. Report calendar dates and displayed payment times use Africa/Cairo; stored payment timestamps are treated as UTC. End dates include the entire selected day, including daylight-saving transitions. Unpaid lists continue to cover all dates and preserve the existing collectible-balance rules.

Changes are marked with `Payment access:` comments. No database migration is needed.

## Edited application files

- SafyaClinic.Application/Interfaces/Services/IPaymentService.cs
- SafyaClinic.Application/Services/PaymentService.cs
- SafyaClinic.Web/Controllers/PaymentsController.cs
- SafyaClinic.Web/Program.cs
- SafyaClinic.Web/Views/Payments/Dashboard.cshtml
- SafyaClinic.Web/Views/Payments/Report.cshtml
- SafyaClinic.Web/Views/Shared/_Layout.cshtml

## Added files

- SafyaClinic.Web/Views/Payments/Unpaid.cshtml
- SafyaClinic.Web/Views/Payments/_UnpaidSections.cshtml
- tests/PaymentAccessChecks/PaymentAccessChecks.csproj
- tests/PaymentAccessChecks/Program.cs
- docs/PaymentAccessChanges.md

## Validation performed

Application and Razor compilation passed. Thirty database-free controller checks passed, covering initial empty reports, server-derived doctor scope, reception date tampering, whole-day boundaries, Cairo spring daylight saving, admin precedence, dashboard query/view selection, and authorization attributes on every payment action.

The existing three nullable-code warnings remain. The test project's restore also reported NU1900 because NuGet vulnerability information could not be fetched. Live authentication, database query execution, and visual browser behavior still need the checks below.

To run the regression checks again from the solution directory:

```powershell
dotnet build tests/PaymentAccessChecks/PaymentAccessChecks.csproj -m:1 -o tests/PaymentAccessChecks/bin/Validation
dotnet tests/PaymentAccessChecks/bin/Validation/PaymentAccessChecks.dll
```

## Manual testing

1. Restart the app. If roles have changed, sign out and back in so the authentication cookie receives the current roles.
2. Admin: open Payments/Dashboard. Verify all cards, source/clinic totals, and drill-downs remain available. Run a report spanning several dates.
3. Reception and ReceptionAdmin, separately: verify Payments opens only the unpaid cards and lists. Verify collection links work for an eligible balance.
4. Under each reception role, open Payments/Report and click Filter. Verify only today's payments appear and no date inputs appear. Manually append `?from=2000-01-01&to=2099-12-31`; verify the result still contains today only.
5. Under each reception role, directly request Payments/DashboardLineDetails?groupType=clinic. Verify access is denied.
6. Doctor: select a date range covering payments linked to at least two doctors. Verify only that doctor's reservation/enrollment transactions appear, including payments collected by reception. Repeat under the second doctor's account. Unlinked historical payments must not appear in either doctor's report.
7. Doctor: directly request Payments/Dashboard and Payments/PatientSummary?patientId=1. Verify access is denied.
8. Verify records paid late on the selected final date appear, and records at the following day's midnight do not. Check report totals against Active transactions; cancelled transactions stay visibly marked and excluded from the total.
