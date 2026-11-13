using SafyaClinic.Application.DTOs.Nutrition;

namespace SafyaClinic.Web.Models;

/// <summary>M4b: one package item on the weekly follow-up form, with what was given (if anything).</summary>
public sealed record FollowUpItemRow(int Index, PackageItemDto Item, RecordAdministeredItemDto? Given, string Kind);
