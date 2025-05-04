using AuthApi.DTOs;
using System.Threading.Tasks;

namespace AuthApi.Services;

public interface IUserService
{
    Task<PagedResult<DoctorResponseDto>> GetAllDoctorsAsync(int pageNumber, int pageSize, string? searchTerm);
    Task<PagedResult<PatientResponseDto>> GetAllPatientsAsync(int pageNumber, int pageSize, string? searchTerm);
    Task<PagedResult<PatientResponseDto>> GetPatientsByDoctorAsync(int doctorId, int pageNumber, int pageSize, string? searchTerm);
    Task<object?> GetUserDetailsAsync(int userId, string role);
}