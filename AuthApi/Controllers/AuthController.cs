using Microsoft.AspNetCore.Mvc;
using AuthApi.Models;
using AuthApi.Data;
using AuthApi.DTOs;
using BCrypt.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using AuthApi.Services;
using System.Security.Cryptography;

namespace AuthApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;
    private readonly IEmailService _emailService;
    private readonly ResetCodeStore _resetCodeStore;
    private readonly List<string> _allowedSpecialties = new List<string>
    {
        "Cardiology",
        "Neurology",
        "Pediatrics",
        "Orthopedics",
        "Dermatology"
    };

    public AuthController(
        AppDbContext context,
        IConfiguration configuration,
        ILogger<AuthController> logger,
        IEmailService emailService,
        ResetCodeStore resetCodeStore)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _emailService = emailService;
        _resetCodeStore = resetCodeStore;
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var emailAddress = new System.Net.Mail.MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task<string?> SaveImageAsync(IFormFile? image, string prefix, int id, string? existingImageUrl = null)
    {
        if (image == null || image.Length == 0)
        {
            _logger.LogInformation("No image provided for {Prefix} with ID {Id}", prefix, id);
            return null;
        }

        const long maxFileSize = 5 * 1024 * 1024; // 5MB
        if (image.Length > maxFileSize)
        {
            _logger.LogWarning("File size exceeds 5MB for {Prefix} with ID {Id}. Size: {FileSize} bytes", prefix, id, image.Length);
            throw new InvalidOperationException("File size exceeds the maximum limit of 5MB.");
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var fileExtension = Path.GetExtension(image.FileName).ToLower();
        if (!allowedExtensions.Contains(fileExtension))
        {
            _logger.LogWarning("Invalid file type for {Prefix} with ID {Id}. Extension: {Extension}", prefix, id, fileExtension);
            throw new InvalidOperationException("Invalid file type. Only JPG, JPEG, PNG, and GIF are allowed.");
        }

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "Images");
        if (!Directory.Exists(uploadsDir))
        {
            _logger.LogInformation("Creating directory: {UploadsDir}", uploadsDir);
            Directory.CreateDirectory(uploadsDir);
        }

        if (!string.IsNullOrEmpty(existingImageUrl))
        {
            var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), existingImageUrl.TrimStart('/'));
            if (System.IO.File.Exists(oldFilePath))
            {
                try
                {
                    System.IO.File.Delete(oldFilePath);
                    _logger.LogInformation("Deleted old image for {Prefix} with ID {Id}: {OldFilePath}", prefix, id, oldFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old image for {Prefix} with ID {Id}: {OldFilePath}", prefix, id, oldFilePath);
                }
            }
        }

        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var fileName = $"{prefix}-{id}-{timestamp}{fileExtension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        _logger.LogInformation("Saving image for {Prefix} with ID {Id} to {FilePath}", prefix, id, filePath);
        try
        {
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save image for {Prefix} with ID {Id} to {FilePath}", prefix, id, filePath);
            throw new InvalidOperationException("Failed to save the image. Please try again.", ex);
        }

        var relativePath = $"/Uploads/Images/{fileName}";
        _logger.LogInformation("Image saved successfully for {Prefix} with ID {Id}. Path: {RelativePath}", prefix, id, relativePath);
        return relativePath;
    }

    private string GenerateJwtToken(string? email, string role, int userId, bool isAdmin)
    {
        if (string.IsNullOrEmpty(email))
        {
            _logger.LogError("Email is null or empty in GenerateJwtToken");
            throw new ArgumentNullException(nameof(email), "Email cannot be null or empty.");
        }

        var jwtSettings = _configuration.GetSection("Jwt");
        var key = jwtSettings["Key"];
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];
        var expiryInMinutes = jwtSettings["ExpiryInMinutes"];

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience) || string.IsNullOrEmpty(expiryInMinutes))
        {
            _logger.LogError("Invalid JWT configuration. Key: {Key}, Issuer: {Issuer}, Audience: {Audience}, ExpiryInMinutes: {ExpiryInMinutes}",
                key, issuer, audience, expiryInMinutes);
            throw new InvalidOperationException("JWT configuration is missing or invalid.");
        }

        if (!double.TryParse(expiryInMinutes, out var expiryMinutes))
        {
            _logger.LogError("Invalid ExpiryInMinutes value: {ExpiryInMinutes}", expiryInMinutes);
            throw new InvalidOperationException("Invalid ExpiryInMinutes value in JWT configuration.");
        }

        var symmetricKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(symmetricKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim("userId", userId.ToString()),
            new Claim("isAdmin", isAdmin.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [HttpPost("admin/register")]
    public async Task<IActionResult> RegisterAdmin([FromBody] AdminDto adminDto)
    {
        try
        {
            _logger.LogInformation("RegisterAdmin called with email: {Email}", adminDto?.Email);

            if (adminDto == null || string.IsNullOrEmpty(adminDto.Email) || string.IsNullOrEmpty(adminDto.Password))
            {
                _logger.LogWarning("Invalid admin data provided");
                return BadRequest(new { message = "Email and password are required" });
            }

            if (!IsValidEmail(adminDto.Email))
            {
                _logger.LogWarning("Invalid email format: {Email}", adminDto.Email);
                return BadRequest(new { message = "Invalid email format" });
            }

            if (await _context.Admins.AnyAsync(a => a.Email == adminDto.Email))
            {
                _logger.LogWarning("Email already exists: {Email}", adminDto.Email);
                return BadRequest(new { message = "Email already exists" });
            }

            var adminCount = await _context.Admins.CountAsync();
            if (adminCount >= 6)
            {
                _logger.LogWarning("Maximum number of admins (6) reached");
                return BadRequest(new { message = "Maximum number of admins (6) has been reached" });
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(adminDto.Password);
            var admin = new Admin
            {
                Email = adminDto.Email,
                PasswordHash = passwordHash
            };

            _context.Admins.Add(admin);
            await _context.SaveChangesAsync();

            var adminResponse = new AdminResponseDto
            {
                Id = admin.Id,
                Email = admin.Email
            };
            _logger.LogInformation("Admin registered successfully with ID: {Id}", admin.Id);
            return Ok(new { message = "Admin registered successfully", admin = adminResponse });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering admin with email {Email}. StackTrace: {StackTrace}", adminDto?.Email, ex.StackTrace);
            return StatusCode(500, new { message = "An error occurred while registering the admin.", details = ex.Message });
        }
    }

    [HttpPost("doctor/register")]
    public async Task<IActionResult> RegisterDoctor([FromForm] DoctorDto doctorDto)
    {
        try
        {
            _logger.LogInformation("RegisterDoctor called with email: {Email}", doctorDto?.Email);

            if (doctorDto == null || string.IsNullOrEmpty(doctorDto.FirstName) || string.IsNullOrEmpty(doctorDto.LastName) ||
                string.IsNullOrEmpty(doctorDto.Gender) || doctorDto.BirthDate == null ||
                string.IsNullOrEmpty(doctorDto.Email) || string.IsNullOrEmpty(doctorDto.Password) ||
                string.IsNullOrEmpty(doctorDto.Specialty))
            {
                _logger.LogWarning("Invalid doctor data provided");
                return BadRequest(new { message = "First name, last name, gender, birth date, email, password, and specialty are required" });
            }

            if (!IsValidEmail(doctorDto.Email))
            {
                _logger.LogWarning("Invalid email format: {Email}", doctorDto.Email);
                return BadRequest(new { message = "Invalid email format" });
            }

            if (!_allowedSpecialties.Contains(doctorDto.Specialty, StringComparer.OrdinalIgnoreCase))
            {
                var allowedSpecialtiesList = string.Join(", ", _allowedSpecialties);
                _logger.LogWarning("Invalid specialty provided: {Specialty}. Allowed specialties are: {AllowedSpecialties}", doctorDto.Specialty, allowedSpecialtiesList);
                return BadRequest(new { message = $"Invalid specialty. Specialty must be one of the following: {allowedSpecialtiesList}" });
            }

            if (await _context.Doctors.AnyAsync(d => d.Email == doctorDto.Email))
            {
                _logger.LogWarning("Email already exists: {Email}", doctorDto.Email);
                return BadRequest(new { message = "Email already exists" });
            }

            var today = DateTime.UtcNow;
            int age = today.Year - doctorDto.BirthDate.Value.Year;
            if (doctorDto.BirthDate.Value.Date > today.AddYears(-age)) age--;

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(doctorDto.Password);
            var doctor = new Doctor
            {
                FirstName = doctorDto.FirstName,
                LastName = doctorDto.LastName,
                Gender = doctorDto.Gender,
                BirthDate = doctorDto.BirthDate,
                Age = age,
                Specialty = doctorDto.Specialty,
                Email = doctorDto.Email,
                PasswordHash = passwordHash
            };

            _context.Doctors.Add(doctor);
            await _context.SaveChangesAsync();

            try
            {
                doctor.ImageUrl = await SaveImageAsync(doctorDto.Image, "doctor", doctor.Id, doctor.ImageUrl);
                await _context.SaveChangesAsync();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Failed to save image for doctor ID {Id}: {Message}", doctor.Id, ex.Message);
                return BadRequest(new { message = ex.Message });
            }

            var doctorResponse = new DoctorResponseDto
            {
                Id = doctor.Id,
                FirstName = doctor.FirstName,
                LastName = doctor.LastName,
                Gender = doctor.Gender,
                BirthDate = doctor.BirthDate,
                Age = doctor.Age,
                Specialty = doctor.Specialty,
                Email = doctor.Email,
                ImageUrl = doctor.ImageUrl
            };
            _logger.LogInformation("Doctor registered successfully with ID: {Id}", doctor.Id);
            return Ok(new { message = "Doctor registered successfully", doctor = doctorResponse });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering doctor with email {Email}. StackTrace: {StackTrace}", doctorDto?.Email, ex.StackTrace);
            return StatusCode(500, new { message = "An error occurred while registering the doctor.", details = ex.Message });
        }
    }

    [HttpPost("patient/register")]
    public async Task<IActionResult> RegisterPatient([FromForm] PatientDto patientDto)
    {
        try
        {
            _logger.LogInformation("RegisterPatient called with email: {Email}", patientDto?.Email);

            if (patientDto == null || string.IsNullOrEmpty(patientDto.FirstName) || string.IsNullOrEmpty(patientDto.LastName) ||
                string.IsNullOrEmpty(patientDto.Gender) || patientDto.BirthDate == null ||
                string.IsNullOrEmpty(patientDto.Email) || string.IsNullOrEmpty(patientDto.Password) ||
                patientDto.DoctorId <= 0 || string.IsNullOrEmpty(patientDto.Diagnosis) ||
                string.IsNullOrEmpty(patientDto.Treatment))
            {
                _logger.LogWarning("Invalid patient data provided: FirstName={FirstName}, LastName={LastName}, Gender={Gender}, BirthDate={BirthDate}, Email={Email}, Password={Password}, DoctorId={DoctorId}, Diagnosis={Diagnosis}, Treatment={Treatment}",
                    patientDto?.FirstName, patientDto?.LastName, patientDto?.Gender, patientDto?.BirthDate, patientDto?.Email, patientDto?.Password, patientDto?.DoctorId, patientDto?.Diagnosis, patientDto?.Treatment);
                return BadRequest(new { message = "First name, last name, gender, birth date, email, password, doctor ID, diagnosis, and treatment are required" });
            }

            if (!IsValidEmail(patientDto.Email))
            {
                _logger.LogWarning("Invalid email format: {Email}", patientDto.Email);
                return BadRequest(new { message = "Invalid email format" });
            }

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == patientDto.DoctorId);
            if (doctor == null)
            {
                _logger.LogWarning("Doctor not found with ID: {DoctorId}", patientDto.DoctorId);
                return BadRequest(new { message = "Doctor not found" });
            }

            if (patientDto.BirthDate == default)
            {
                _logger.LogWarning("Invalid BirthDate provided: {BirthDate}", patientDto.BirthDate);
                return BadRequest(new { message = "Invalid BirthDate" });
            }

            var today = DateTime.UtcNow;
            int age = today.Year - patientDto.BirthDate.Value.Year;
            if (patientDto.BirthDate.Value.Date > today.AddYears(-age)) age--;

            Patient patient;
            var existingPatient = await _context.Patients.FirstOrDefaultAsync(p => p.Email == patientDto.Email);
            if (existingPatient == null)
            {
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(patientDto.Password);
                patient = new Patient
                {
                    FirstName = patientDto.FirstName,
                    LastName = patientDto.LastName,
                    Gender = patientDto.Gender,
                    BirthDate = patientDto.BirthDate,
                    Age = age,
                    BloodType = patientDto.BloodType ?? "A+",
                    Email = patientDto.Email,
                    PasswordHash = passwordHash,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Patients.Add(patient);
            }
            else
            {
                if (!BCrypt.Net.BCrypt.Verify(patientDto.Password, existingPatient.PasswordHash))
                {
                    _logger.LogWarning("Invalid password for existing patient with email: {Email}", patientDto.Email);
                    return BadRequest(new { message = "Invalid password for existing patient" });
                }
                patient = existingPatient;
                patient.Age = age;
                patient.BloodType = patientDto.BloodType ?? "A+";
            }

            if (patient.Id != 0)
            {
                var existingHistory = await _context.PatientHistories
                    .AnyAsync(ph => ph.PatientId == patient.Id && ph.DoctorId == doctor.Id);
                if (existingHistory)
                {
                    _logger.LogWarning("Patient with ID {PatientId} already has a history with doctor ID {DoctorId}", patient.Id, doctor.Id);
                    return BadRequest(new { message = "Patient already has a treatment history with this doctor" });
                }
            }

            var patientHistory = new PatientHistory
            {
                Patient = patient,
                DoctorId = doctor.Id,
                Diagnosis = patientDto.Diagnosis,
                Treatment = patientDto.Treatment,
                TreatmentDate = DateTime.UtcNow
            };

            if (patient.Id == 0)
            {
                patient.PatientHistories = new List<PatientHistory> { patientHistory };
            }
            else
            {
                patient.PatientHistories.Add(patientHistory);
                _context.PatientHistories.Add(patientHistory);
            }

            _logger.LogInformation("Attempting to save patient with email: {Email}", patientDto.Email);
            await _context.SaveChangesAsync();

            try
            {
                patient.ImageUrl = await SaveImageAsync(patientDto.Image, "patient", patient.Id, patient.ImageUrl);
                patient.XRayImageUrl = await SaveImageAsync(patientDto.XRayImage, "patient-xray", patient.Id, patient.XRayImageUrl);
                patient.LabResultsImageUrl = await SaveImageAsync(patientDto.LabResultsImage, "patient-lab", patient.Id, patient.LabResultsImageUrl);
                await _context.SaveChangesAsync();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Failed to save images for patient ID {Id}: {Message}", patient.Id, ex.Message);
                return BadRequest(new { message = ex.Message });
            }

            var treatmentHistories = patient.PatientHistories.Select(ph => new TreatmentHistoryDto
            {
                Diagnosis = ph.Diagnosis,
                Treatment = ph.Treatment,
                DoctorId = ph.DoctorId,
                DoctorFirstName = ph.Doctor.FirstName,
                DoctorLastName = ph.Doctor.LastName
            }).ToList();

            var patientResponse = new PatientResponseDto
            {
                Id = patient.Id,
                FirstName = patient.FirstName,
                LastName = patient.LastName,
                Gender = patient.Gender,
                BirthDate = patient.BirthDate,
                Age = patient.Age,
                BloodType = patient.BloodType,
                Email = patient.Email,
                ImageUrl = patient.ImageUrl,
                XRayImageUrl = patient.XRayImageUrl, 
                LabResultsImageUrl = patient.LabResultsImageUrl, 
                CreatedAt = patient.CreatedAt,
                Department = treatmentHistories
            };

            _logger.LogInformation("Patient registered successfully with ID: {Id}", patient.Id);
            return Ok(new { message = "Patient registered successfully", patient = patientResponse });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering patient with email {Email}. StackTrace: {StackTrace}", patientDto?.Email, ex.StackTrace);
            return StatusCode(500, new { message = "An error occurred while registering the patient.", details = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            _logger.LogInformation("Login attempt for email: {Email}", loginDto?.Email);

            if (loginDto == null || string.IsNullOrEmpty(loginDto.Email) || string.IsNullOrEmpty(loginDto.Password))
            {
                _logger.LogWarning("Invalid login data provided");
                return BadRequest(new { message = "Email and password are required" });
            }

            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Email == loginDto.Email);
            if (admin != null)
            {
                bool isPasswordValid;
                try
                {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, admin.PasswordHash);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error verifying password for admin with email: {Email}. StackTrace: {StackTrace}", loginDto.Email, ex.StackTrace);
                    return StatusCode(500, new { message = "An error occurred while verifying the admin password.", details = ex.Message });
                }

                if (!isPasswordValid)
                {
                    _logger.LogWarning("Invalid password for admin with email: {Email}", loginDto.Email);
                    return BadRequest(new { message = "Invalid email or password" });
                }

                var token = GenerateJwtToken(admin.Email, "admin", admin.Id, true);
                _logger.LogInformation("Admin login successful for email: {Email}", admin.Email);
                return Ok(new
                {
                    message = "Login successful",
                    role = "admin",
                    userId = admin.Id,
                    isAdmin = true,
                    token
                });
            }

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Email == loginDto.Email);
            if (doctor != null)
            {
                bool isPasswordValid;
                try
                {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, doctor.PasswordHash);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error verifying password for doctor with email: {Email}. StackTrace: {StackTrace}", loginDto.Email, ex.StackTrace);
                    return StatusCode(500, new { message = "An error occurred while verifying the doctor password.", details = ex.Message });
                }

                if (!isPasswordValid)
                {
                    _logger.LogWarning("Invalid password for doctor with email: {Email}", loginDto.Email);
                    return BadRequest(new { message = "Invalid email or password" });
                }

                var token = GenerateJwtToken(doctor.Email, "doctor", doctor.Id, false);
                _logger.LogInformation("Doctor login successful for email: {Email}", doctor.Email);
                return Ok(new
                {
                    message = "Login successful",
                    role = "doctor",
                    userId = doctor.Id,
                    isAdmin = false,
                    token
                });
            }

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Email == loginDto.Email);
            if (patient != null)
            {
                bool isPasswordValid;
                try
                {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, patient.PasswordHash);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error verifying password for patient with email: {Email}. StackTrace: {StackTrace}", loginDto.Email, ex.StackTrace);
                    return StatusCode(500, new { message = "An error occurred while verifying the patient password.", details = ex.Message });
                }

                if (!isPasswordValid)
                {
                    _logger.LogWarning("Invalid password for patient with email: {Email}", loginDto.Email);
                    return BadRequest(new { message = "Invalid email or password" });
                }

                var token = GenerateJwtToken(patient.Email, "patient", patient.Id, false);
                _logger.LogInformation("Patient login successful for email: {Email}", patient.Email);
                return Ok(new
                {
                    message = "Login successful",
                    role = "patient",
                    userId = patient.Id,
                    isAdmin = false,
                    token
                });
            }

            _logger.LogWarning("Invalid login attempt for email: {Email}", loginDto.Email);
            return BadRequest(new { message = "Invalid email or password" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login attempt for email {Email}. StackTrace: {StackTrace}", loginDto?.Email, ex.StackTrace);
            return StatusCode(500, new { message = "An error occurred during login.", details = ex.Message });
        }
    }

    [HttpPost("reset-database")]
    public async Task<IActionResult> ResetDatabase()
    {
        try
        {
            _logger.LogInformation("Resetting database...");

            _context.PatientHistories.RemoveRange(_context.PatientHistories);
            await _context.SaveChangesAsync();

            _context.Patients.RemoveRange(_context.Patients);
            _context.Doctors.RemoveRange(_context.Doctors);
            _context.Admins.RemoveRange(_context.Admins);
            await _context.SaveChangesAsync();

            await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Admins', RESEED, 0)");
            await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Doctors', RESEED, 0)");
            await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Patients', RESEED, 0)");
            await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('PatientHistories', RESEED, 0)");

            var admin1 = new Admin
            {
                Email = "admin1@gedisa.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!")
            };
            var admin2 = new Admin
            {
                Email = "admin2@gedisa.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin456!")
            };

            _context.Admins.AddRange(admin1, admin2);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Database reset successfully. Added admin users with Id 1 and 2.");
            return Ok(new { message = "Database reset successfully. Added admin users with Id 1 and 2." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting database. StackTrace: {StackTrace}", ex.StackTrace);
            return StatusCode(500, new { message = "An error occurred while resetting the database.", details = ex.Message });
        }
    }

    private string GenerateResetCode()
    {
        using var rng = RandomNumberGenerator.Create();
        var codeBytes = new byte[4];
        rng.GetBytes(codeBytes);
        return (BitConverter.ToUInt32(codeBytes) % 1000000).ToString("D6");
    }

    private string BuildEmailBody(string resetCode)
    {
        return $@"
    <!DOCTYPE html>
    <html>
    <head>
        <style>
            body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #E0F2F1; }}
            .container {{ max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 10px; box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1); }}
            .logo {{ text-align: center; margin-bottom: 20px; }}
            .code {{ font-size: 24px; font-weight: bold; color: #1A3C34; text-align: center; margin: 20px 0; padding: 15px; background-color: #B2DFDB; border-radius: 5px; }}
            .note {{ color: #4CAF50; font-size: 14px; margin-top: 20px; }}
        </style>
    </head>
    <body>
        <div class='container'>
            <div class='logo'>
                <img src='https://i.imgur.com/h1yMyiQ.png' alt='Cura Logo' style='max-width: 150px;'>
            </div>
            <h2 style='color: #26A69A; text-align: center;'>Password Reset Request</h2>
            <p style='color: #1A3C34;'>We received a request to reset your Cura Health account password. Here's your verification code:</p>
            <div class='code'>{resetCode}</div>
            <p style='color: #1A3C34;'>This code will expire in 15 minutes. If you didn't request this reset, please ignore this email.</p>
            <div class='note'>
                <p>For security reasons:</p>
                <ul>
                    <li>Never share this code with anyone</li>
                    <li>Cura Health will never ask for your password</li>
                    <li>Delete this email after use</li>
                </ul>
            </div>
        </div>
    </body>
    </html>";
    }

    [HttpPost("request-password-reset")]
    public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequestDto requestDto)
    {
        try
        {
            _logger.LogInformation("RequestPasswordReset called for email: {Email}", requestDto?.Email);

            if (requestDto == null || string.IsNullOrEmpty(requestDto.Email))
            {
                _logger.LogWarning("Invalid email provided for password reset");
                return BadRequest(new { message = "Email is required" });
            }

            if (!IsValidEmail(requestDto.Email))
            {
                _logger.LogWarning("Invalid email format: {Email}", requestDto.Email);
                return BadRequest(new { message = "Invalid email format" });
            }

            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Email == requestDto.Email);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Email == requestDto.Email);
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Email == requestDto.Email);

            if (admin == null && doctor == null && patient == null)
            {
                _logger.LogWarning("Email not found: {Email}", requestDto.Email);
                return NotFound(new { message = "Email not found" });
            }

            var resetCode = GenerateResetCode();
            _resetCodeStore.StoreCode(requestDto.Email, resetCode, TimeSpan.FromMinutes(15));

            try
            {
                await _emailService.SendEmailAsync(requestDto.Email, "Password Reset Request - Cura Health", BuildEmailBody(resetCode), isHtml: true);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", requestDto.Email);
                return StatusCode(500, new { message = "Failed to send reset code email. Please try again." });
            }

            _logger.LogInformation("Password reset code sent to email: {Email}", requestDto.Email);
            return Ok(new { message = "Password reset code sent to your email" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting password reset for email {Email}", requestDto?.Email);
            return StatusCode(500, new { message = "An error occurred while requesting the password reset.", details = ex.Message });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] PasswordResetDto resetDto)
    {
        try
        {
            _logger.LogInformation("ResetPassword called for email: {Email}", resetDto?.Email);

            if (resetDto == null || string.IsNullOrEmpty(resetDto.Email) || string.IsNullOrEmpty(resetDto.ResetCode) || string.IsNullOrEmpty(resetDto.NewPassword))
            {
                _logger.LogWarning("Invalid data provided for password reset");
                return BadRequest(new { message = "Email, reset code, and new password are required" });
            }

            bool isExpired;
            if (!_resetCodeStore.ValidateCode(resetDto.Email, resetDto.ResetCode, out isExpired))
            {
                if (isExpired)
                {
                    _logger.LogWarning("Reset code has expired for email: {Email}", resetDto.Email);
                    return BadRequest(new { message = "Reset code has expired" });
                }
                _logger.LogWarning("Invalid reset code for email: {Email}", resetDto.Email);
                return BadRequest(new { message = "Invalid reset code" });
            }

            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.Email == resetDto.Email);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Email == resetDto.Email);
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Email == resetDto.Email);

            if (admin == null && doctor == null && patient == null)
            {
                _logger.LogWarning("Email not found: {Email}", resetDto.Email);
                return NotFound(new { message = "Email not found" });
            }

            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(resetDto.NewPassword);
            if (admin != null)
            {
                admin.PasswordHash = newPasswordHash;
            }
            else if (doctor != null)
            {
                doctor.PasswordHash = newPasswordHash;
            }
            else if (patient != null)
            {
                patient.PasswordHash = newPasswordHash;
            }

            await _context.SaveChangesAsync();
            _resetCodeStore.RemoveCode(resetDto.Email);

            _logger.LogInformation("Password reset successfully for email: {Email}", resetDto.Email);
            return Ok(new { message = "Password reset successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for email {Email}", resetDto?.Email);
            return StatusCode(500, new { message = "An error occurred while resetting the password.", details = ex.Message });
        }
    }
}