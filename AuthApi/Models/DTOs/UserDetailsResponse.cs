namespace AuthApi.DTOs;

public class UserDetailsResponse
{
    public string Role { get; set; } = string.Empty;
    public object? Details { get; set; }
}