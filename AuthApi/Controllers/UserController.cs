using AuthApi.Data;
using AuthApi.DTOs;
using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AuthApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;
    private readonly AppDbContext _context; 

    public UserController(IUserService userService, ILogger<UserController> logger, AppDbContext context)
    {
        _userService = userService;
        _logger = logger;
        _context = context; 
    }

    [Authorize(Roles = "admin")]
    [HttpGet("doctors")]
    public async Task<IActionResult> GetAllDoctors([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? searchTerm = null)
    {
        try
        {
            _logger.LogInformation("GetAllDoctors called with pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}", pageNumber, pageSize, searchTerm);

            if (pageNumber < 1 || pageSize < 1)
            {
                _logger.LogWarning("Invalid pagination parameters: pageNumber={PageNumber}, pageSize={PageSize}", pageNumber, pageSize);
                return BadRequest(new { message = "Page number and page size must be greater than 0" });
            }

            var result = await _userService.GetAllDoctorsAsync(pageNumber, pageSize, searchTerm);
            _logger.LogInformation("Successfully retrieved {Count} doctors", result.Data.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving doctors. StackTrace: {StackTrace}", ex.StackTrace);
            return StatusCode(500, new { message = "An error occurred while retrieving doctors.", details = ex.Message });
        }
    }

    [Authorize(Roles = "admin")]
    [HttpGet("patients")]
    public async Task<IActionResult> GetAllPatients([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? searchTerm = null)
    {
        try
        {
            _logger.LogInformation("GetAllPatients called with pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}", pageNumber, pageSize, searchTerm);

            if (pageNumber < 1 || pageSize < 1)
            {
                _logger.LogWarning("Invalid pagination parameters: pageNumber={PageNumber}, pageSize={PageSize}", pageNumber, pageSize);
                return BadRequest(new { message = "Page number and page size must be greater than 0" });
            }

            var result = await _userService.GetAllPatientsAsync(pageNumber, pageSize, searchTerm);
            _logger.LogInformation("Successfully retrieved {Count} patients", result.Data.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving patients. StackTrace: {StackTrace}", ex.StackTrace);
            return StatusCode(500, new { message = "An error occurred while retrieving patients.", details = ex.Message });
        }
    }

    [Authorize(Roles = "doctor,admin")]
    [HttpGet("patients/doctor/{doctorId}")]
    public async Task<IActionResult> GetPatientsByDoctor(int doctorId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] string? searchTerm = null)
    {
        try
        {
            _logger.LogInformation("GetPatientsByDoctor called with doctorId: {DoctorId}, pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}", doctorId, pageNumber, pageSize, searchTerm);

            if (doctorId <= 0)
            {
                _logger.LogWarning("Invalid doctorId: {DoctorId}", doctorId);
                return BadRequest(new { message = "Doctor ID must be greater than 0" });
            }

            if (pageNumber < 1 || pageSize < 1)
            {
                _logger.LogWarning("Invalid pagination parameters: pageNumber={PageNumber}, pageSize={PageSize}", pageNumber, pageSize);
                return BadRequest(new { message = "Page number and page size must be greater than 0" });
            }

            var userIdClaim = User.FindFirst("userId")?.Value;
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId) || string.IsNullOrEmpty(roleClaim))
            {
                _logger.LogWarning("Invalid token claims: userId={UserId}, role={Role}", userIdClaim, roleClaim);
                return Unauthorized(new { message = "Invalid token claims" });
            }

            if (roleClaim == "doctor" && userId != doctorId)
            {
                _logger.LogWarning("Unauthorized access attempt by doctor userId: {UserId} to doctorId: {DoctorId}", userId, doctorId);
                return Unauthorized(new { message = "You are not authorized to access this doctor's patients." });
            }

            var result = await _userService.GetPatientsByDoctorAsync(doctorId, pageNumber, pageSize, searchTerm);
            _logger.LogInformation("Successfully retrieved {Count} patients for doctorId: {DoctorId}", result.Data.Count, doctorId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving patients for doctorId: {DoctorId}. StackTrace: {StackTrace}", doctorId, ex.StackTrace);
            return StatusCode(500, new { message = "An error occurred while retrieving patients.", details = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("details")]
    public async Task<IActionResult> GetMyDetails()
    {
        try
        {
            _logger.LogInformation("GetMyDetails called for logged-in user");

            var userIdClaim = User.FindFirst("userId")?.Value;
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;

            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId) || string.IsNullOrEmpty(roleClaim))
            {
                _logger.LogWarning("Invalid token claims: userId={UserId}, role={Role}", userIdClaim, roleClaim);
                return Unauthorized(new { message = "Invalid token claims" });
            }

            var userDetails = await _userService.GetUserDetailsAsync(userId, roleClaim);
            if (userDetails == null)
            {
                _logger.LogWarning("User not found: userId={UserId}, role={Role}", userId, roleClaim);
                return NotFound(new { message = "User not found" });
            }

            if (roleClaim == "patient" && userDetails is PatientResponseDto patientResponse)
            {
                var patient = await _context.Patients.FindAsync(userId);
                if (patient != null)
                {
                    patientResponse.XRayImageUrl = patient.XRayImageUrl;
                    patientResponse.LabResultsImageUrl = patient.LabResultsImageUrl;
                }
            }

            _logger.LogInformation("Successfully retrieved details for userId: {UserId}, role: {Role}", userId, roleClaim);
            return Ok(new { message = "User details retrieved successfully", user = userDetails });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user details for logged-in user. StackTrace: {StackTrace}", ex.StackTrace);
            return StatusCode(500, new { message = "An error occurred while retrieving user details.", details = ex.Message });
        }
    }
}