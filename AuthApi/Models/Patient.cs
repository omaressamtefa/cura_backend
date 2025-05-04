namespace AuthApi.Models;

public class Patient
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public int Age { get; set; }
    public string BloodType { get; set; } = "A+";
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? ImageUrl { get; set; } 
    public string? XRayImageUrl { get; set; } 
    public string? LabResultsImageUrl { get; set; } 
    public DateTime CreatedAt { get; set; }
    public List<PatientHistory> PatientHistories { get; set; } = new List<PatientHistory>();
}