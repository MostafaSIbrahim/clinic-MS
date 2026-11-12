using SafyaClinic.Domain.Enums;

namespace SafyaClinic.Application.Clinical;

/// <summary>P9a: the prescription route list: the value stored, and the English label shown (translated in views).</summary>
public static class AdministrationRoutes
{
    public static readonly IReadOnlyList<(string Value, string Label)> All = new[]
    {
        (nameof(AdministrationRoute.Oral), "Oral"),
        (nameof(AdministrationRoute.Sublingual), "Sublingual"),
        (nameof(AdministrationRoute.Intravenous), "Intravenous (IV)"),
        (nameof(AdministrationRoute.Intramuscular), "Intramuscular (IM)"),
        (nameof(AdministrationRoute.Subcutaneous), "Subcutaneous (SC)"),
        (nameof(AdministrationRoute.Topical), "Topical"),
        (nameof(AdministrationRoute.Inhaled), "Inhaled"),
        (nameof(AdministrationRoute.Nasal), "Nasal"),
        (nameof(AdministrationRoute.Eye), "Eye"),
        (nameof(AdministrationRoute.Ear), "Ear"),
        (nameof(AdministrationRoute.Rectal), "Rectal"),
        (nameof(AdministrationRoute.Vaginal), "Vaginal"),
        (nameof(AdministrationRoute.Transdermal), "Transdermal"),
        (nameof(AdministrationRoute.Other), "Other route"),
    };

    /// <summary>Empty (no route given) or one of the list. Typed text is no longer accepted for new drugs.</summary>
    public static bool IsAllowed(string? value) =>
        string.IsNullOrWhiteSpace(value) || All.Any(r => r.Value == value.Trim());

    /// <summary>The label for a stored route; a route typed before P9a is shown as it was typed.</summary>
    public static string? Label(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return null;
        var match = All.FirstOrDefault(r => r.Value == stored.Trim());
        return match.Value is null ? stored : match.Label;
    }

    public const string NotInList = "Choose the route from the list.";
}
