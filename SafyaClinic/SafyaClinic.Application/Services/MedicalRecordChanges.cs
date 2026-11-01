using SafyaClinic.Application.DTOs.MedicalRecord;

namespace SafyaClinic.Application.Services;

/// <summary>
/// Names of the fields a medical-record edit changes (F3b-2). The audit trail records these
/// names instead of the clinical text itself. Text is compared trimmed, as the edit stores it.
/// </summary>
public static class MedicalRecordChanges
{
    public static IReadOnlyList<string> ChangedFields(UpdatePatientRecordRequest current, UpdatePatientRecordRequest update)
    {
        var changed = new List<string>();

        void Text(string name, string? before, string? after)
        {
            if ((before?.Trim() ?? string.Empty) != (after?.Trim() ?? string.Empty))
                changed.Add(name);
        }

        Text(nameof(update.ChiefComplaint), current.ChiefComplaint, update.ChiefComplaint);
        Text(nameof(update.PresentIllnessHistory), current.PresentIllnessHistory, update.PresentIllnessHistory);
        Text(nameof(update.Diagnosis), current.Diagnosis, update.Diagnosis);
        Text(nameof(update.DifferentialDiagnosis), current.DifferentialDiagnosis, update.DifferentialDiagnosis);
        Text(nameof(update.TreatmentPlan), current.TreatmentPlan, update.TreatmentPlan);
        Text(nameof(update.Notes), current.Notes, update.Notes);
        if (current.FollowUpDate != update.FollowUpDate)
            changed.Add(nameof(update.FollowUpDate));

        return changed;
    }
}