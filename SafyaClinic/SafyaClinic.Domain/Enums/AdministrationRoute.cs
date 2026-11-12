namespace SafyaClinic.Domain.Enums;

/// <summary>P9a: how a prescribed drug is given. Stored by name in PrescriptionItem.RouteOfAdministration (text),
/// so routes typed before P9a are kept and still shown.</summary>
public enum AdministrationRoute
{
    Oral,
    Sublingual,
    Intravenous,
    Intramuscular,
    Subcutaneous,
    Topical,
    Inhaled,
    Nasal,
    Eye,
    Ear,
    Rectal,
    Vaginal,
    Transdermal,
    Other
}
