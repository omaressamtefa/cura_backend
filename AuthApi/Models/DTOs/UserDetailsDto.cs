namespace AuthApi.DTOs;

public class UserDetailsDto
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? Specialty { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? Age { get; set; }
    public string? BloodType { get; set; }
    public DateTime? PatientCreatedAt { get; set; }
    public List<TreatmentHistoryDto> Department { get; set; } = new List<TreatmentHistoryDto>();
}