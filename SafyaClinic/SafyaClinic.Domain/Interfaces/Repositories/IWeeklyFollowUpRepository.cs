

using SafyaClinic.Domain.Entities.Nutrition;

namespace SafyaClinic.Domain.Interfaces.Repositories
{
    public interface IWeeklyFollowUpRepository: IRepository<Entities.Nutrition.WeeklyFollowUp>
    {
        Task<IEnumerable<Entities.Nutrition.WeeklyFollowUp>> GetByEnrollmentAsync(int enrollmentId);
        Task<int?> GetMaxWeekNumberAsync(int enrollmentId);
        Task<Entities.Nutrition.WeeklyFollowUp?> GetFollowUpWithDetailsAsync(int followUpId);
        Task<IEnumerable<WeeklyAdministeredItem>> GetAdministeredItemsWithDetailsAsync(
    int followUpId);

        Task<IEnumerable<WeeklyFollowUpLabResult>> GetLabResultsWithDetailsAsync(
            int followUpId);
    }
}
