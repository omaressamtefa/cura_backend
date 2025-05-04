namespace AuthApi.DTOs;

public class PatientDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string? BloodType { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int DoctorId { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string Treatment { get; set; } = string.Empty;
    public IFormFile? Image { get; set; } 
    public IFormFile? XRayImage { get; set; } 
    public IFormFile? LabResultsImage { get; set; } /