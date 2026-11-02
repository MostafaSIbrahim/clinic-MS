using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Entities.MedicalRecord;
using SafyaClinic.Domain.Entities.Nutrition;
using SafyaClinic.Domain.Entities.Payment;
using SafyaClinic.Domain.Entities.Prescription;
using SafyaClinic.Infrastructure.Data;
using Xunit;

namespace SafyaClinic.Tests.Infrastructure;

/// <summary>F5a / F20: the SQL Server model has no shadow keys and protects history. No database is contacted.</summary>
public class ModelIntegrityTests
{
    private static readonly IModel Model = new SafyaDbContext(new DbContextOptionsBuilder<SafyaDbContext>()
        .UseSqlServer("Server=model-only;Database=ModelOnly;Integrated Security=True").Options).Model;

    [Theory]
    [InlineData(typeof(MedicalAnalysis), "AnalysisTypeId1")]
    [InlineData(typeof(WeeklyAdministeredItem), "PackageItemId1")]
    [InlineData(typeof(Prescription), "PatientRecordId")]
    public void Shadow_key_columns_are_gone(Type entity, string column)
    {
        Assert.Null(Model.FindEntityType(entity)!.FindProperty(column));
    }

    [Theory]
    [InlineData(typeof(Payment), "CollectedBy")]
    [InlineData(typeof(PatientNutritionEnrollment), "PatientId")]
    [InlineData(typeof(PatientNutritionEnrollment), "PackageId")]
    [InlineData(typeof(PatientNutritionEnrollment), "DoctorId")]
    [InlineData(typeof(PaymentAdjustment), "PaymentId")]
    [InlineData(typeof(Prescription), "RecordId")]
    [InlineData(typeof(Treatment), "RecordId")]
    [InlineData(typeof(WeeklyFollowUpLabResult), "AnalysisTypeId")]
    public void History_is_never_deleted_by_cascade(Type entity, string foreignKey)
    {
        var fk = Assert.Single(Model.FindEntityType(entity)!.GetForeignKeys(),
            k => k.Properties.Count == 1 && k.Properties[0].Name == foreignKey);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Theory]
    [InlineData(typeof(AnalysisAttachment))]
    [InlineData(typeof(PrescriptionAttachment))]
    public void Content_type_holds_100_characters(Type entity)
    {
        Assert.Equal(100, Model.FindEntityType(entity)!.FindProperty("ContentType")!.GetMaxLength());
    }
}