using Microsoft.AspNetCore.Http;

namespace AuthApi.DTOs;

public class UpdatePatientDto
{
    public string? FirstName { get; set; } 
    public DateTime? BirthDate { get; set; } 
    public int? Age { get; set; } 
    public string? BloodType { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public int? DoctorId { get; set; } 
    public string? Diagnosis { get; set; } 
    public string? Treatment { get; set; } 
    public IFormFile? Image { get; set; }
}