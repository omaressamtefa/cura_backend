using Microsoft.AspNetCore.Http;
using AuthApi.Models;

namespace AuthApi.DTOs;

public class RegisterDto
{
    public string Role { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Password { get; set; }

    public string? Name { get; set; }
    
  
    
    public DateTime? DateOfBirth { get; set; } 
  
    public int? DoctorId { get; set; } 
    public IFormFile? Image { get; set; } 
}