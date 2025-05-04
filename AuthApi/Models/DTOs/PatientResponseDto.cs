namespace AuthApi.DTOs;

public class PatientResponseDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public int Age { get; set; }
    public string BloodType { get; set; } = "A+";
    public string Email { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? XRayImageUrl { get; set; } 
    public string? LabResultsImageUrl { get; set; } 
    public DateTime CreatedAt { get; set; }
    public List<TreatmentHistoryDto> Department { get; set; } = new List<TreatmentHistoryDto>();
}

public class TreatmentHistoryDto
{
    public string Diagnosis { get; set; } = string.Empty;
    public string Treatment { get; set; } = string.Empty;
    public int DoctorId { get; set; }
    public string? DoctorFirstName { get; set; }
    public string? DoctorLastName { get; set; }
}