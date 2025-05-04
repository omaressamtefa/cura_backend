namespace AuthApi.DTOs;

public class PatientUpdateDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public int? DoctorId { get; set; }
    public string? Diagnosis { get; set; }
    public string? Treatment { get; set; }
    public IFormFile? Image { get; set; } 
    public IFormFile? XRayImage { get; set; } 
    public IFormFile? LabResultsImage { get; set; } 
}