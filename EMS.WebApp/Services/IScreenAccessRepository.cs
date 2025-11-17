namespace EMS.WebApp.Services
{
    public interface IScreenAccessRepository
    {
        Task<bool> HasScreenAccessAsync(string userUserName, string screenName);
    }
}


