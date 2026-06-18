

namespace SafyaClinic.Domain.Interfaces.Repositories
{
    public interface IWeeklyFollowUpRepository: IRepository<Entities.Nutrition.WeeklyFollowUp>
    {
        Task<IEnumerable<Entities.Nutrition.WeeklyFollowUp>> GetByEnrollmentAsync(int enrollmentId);
        Task<Entities.Nutrition.WeeklyFollowUp?> GetFollowUpWithDetailsAsync(int followUpId);
    }
}
