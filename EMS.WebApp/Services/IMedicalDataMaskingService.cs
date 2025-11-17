namespace EMS.WebApp.Services
{
    public interface IMedicalDataMaskingService
    {
        bool ShouldMaskData(string? userRole);
        string MaskValue(string? value);
        T MaskObject<T>(T obj, string? userRole) where T : class;
    }
}
