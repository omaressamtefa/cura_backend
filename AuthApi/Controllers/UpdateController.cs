using AuthApi.Data;
using AuthApi.DTOs;
using AuthApi.Models;
using AuthApi.Services;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AuthApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UpdateController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IImageService _imageService;
    private readonly ILogger<UpdateController> _logger;

    public UpdateController(AppDbContext context, IImageService imageService, ILogger<UpdateController> logger)
    {
        _context = context;
        _imageService = imageService;
        _logger = logger;
    }

    [Authorize(Roles = "admin")]
    [HttpPut("doctor/{id}")]
    public async Task<IActionResult> UpdateDoctor(int id, [FromForm] UpdateDoctorDto updateDoctorDto)
    {
        _logger.LogInformation("UpdateDoctor called for doctor ID {Id}", id);

        var doctor = await _context.Doctors.FindAsync(id);
        if (doctor == null)
        {
            _logger.LogWarning("Doctor not found with ID: {Id}", id);
            return NotFound(new { message = "Doctor not found" });
        }

        if (!string.IsNullOrEmpty(updateDoctorDto.Email) && updateDoctorDto.Email != doctor.Email)
        {
            if (await _context.Doctors.AnyAsync(d => d.Email == updateDoctorDto.Email))
            {
                _logger.LogWarning("Email already exists: {Email}", updateDoctorDto.Email);
                return BadRequest(new { message = "Email already exists" });
            }
            doctor.Email = updateDoctorDto.Email;
        }

        // Update fields if provided
        if (!string.IsNullOrEmpty(updateDoctorDto.FirstName))
        {
            doctor.FirstName = updateDoctorDto.FirstName;
        }

        if (!string.IsNullOrEmpty(updateDoctorDto.LastName))
        {
            doctor.LastName = updateDoctorDto.LastName;
        }

        if (!string.IsNullOrEmpty(updateDoctorDto.Gender))
        {
            doctor.Gender = updateDoctorDto.Gender;
        }

        if (updateDoctorDto.BirthDate.HasValue)
        {
            doctor.BirthDate = updateDoctorDto.BirthDate.Value;
        }

        if (!string.IsNullOrEmpty(updateDoctorDto.Specialty))
        {
            doctor.Specialty = updateDoctorDto.Specialty;
        }

        if (!string.IsNullOrEmpty(updateDoctorDto.Password))
        {
            doctor.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updateDoctorDto.Password);
        }

        // Update the image if provided
        if (updateDoctorDto.Image != null)
        {
            try
            {
                var newImageUrl = await _imageService.SaveImageAsync(updateDoctorDto.Image, "doctor", id, doctor.ImageUrl);
                if (newImageUrl != null)
                {
                    doctor.ImageUrl = newImageUrl;
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to update image for doctor ID {Id}: {Message}", id, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update doctor ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while updating the doctor." });
        }

        var doctorResponse = new DoctorResponseDto
        {
            Id = doctor.Id,
            FirstName = doctor.FirstName,
            LastName = doctor.LastName,
            Gender = doctor.Gender,
            BirthDate = doctor.BirthDate,
            Specialty = doctor.Specialty,
            Email = doctor.Email,
            ImageUrl = doctor.ImageUrl
        };

        _logger.LogInformation("Doctor updated successfully for ID {Id}", id);
        return Ok(new { message = "Doctor updated successfully", doctor = doctorResponse });
    }

    [Authorize(Roles = "admin")]
    [HttpPut("patient/{id}")]
    public async Task<IActionResult> UpdatePatient(int id, [FromForm] PatientUpdateDto updatePatientDto)
    {
        try
        {
            _logger.LogInformation("UpdatePatient called for patient ID: {Id}", id);

            var patient = await _context.Patients
                .Include(p => p.PatientHistories)
                .ThenInclude(ph => ph.Doctor)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (patient == null)
            {
                _logger.LogWarning("Patient not found with ID: {Id}", id);
                return NotFound(new { message = "Patient not found" });
            }

            // Update patient details if provided
            if (!string.IsNullOrEmpty(updatePatientDto.FirstName))
            {
                patient.FirstName = updatePatientDto.FirstName;
            }

            if (!string.IsNullOrEmpty(updatePatientDto.LastName))
            {
                patient.LastName = updatePatientDto.LastName;
            }

            if (!string.IsNullOrEmpty(updatePatientDto.Gender))
            {
                patient.Gender = updatePatientDto.Gender;
            }

            if (updatePatientDto.BirthDate.HasValue)
            {
                patient.BirthDate = updatePatientDto.BirthDate.Value;
            }

            if (!string.IsNullOrEmpty(updatePatientDto.Email))
            {
                if (_context.Patients.Any(p => p.Email == updatePatientDto.Email && p.Id != id))
                {
                    _logger.LogWarning("Email already exists: {Email}", updatePatientDto.Email);
                    return BadRequest(new { message = "Email already exists" });
                }
                patient.Email = updatePatientDto.Email;
            }

            if (!string.IsNullOrEmpty(updatePatientDto.Password))
            {
                patient.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updatePatientDto.Password);
            }

            // Update PatientHistory if doctor, diagnosis, or treatment is provided
            if (updatePatientDto.DoctorId > 0 || !string.IsNullOrEmpty(updatePatientDto.Diagnosis) || !string.IsNullOrEmpty(updatePatientDto.Treatment))
            {
                if (updatePatientDto.DoctorId <= 0 || string.IsNullOrEmpty(updatePatientDto.Diagnosis) || string.IsNullOrEmpty(updatePatientDto.Treatment))
                {
                    _logger.LogWarning("DoctorId, Diagnosis, and Treatment must all be provided to update patient history");
                    return BadRequest(new { message = "DoctorId, Diagnosis, and Treatment must all be provided to update patient history" });
                }

                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == updatePatientDto.DoctorId);
                if (doctor == null)
                {
                    _logger.LogWarning("Doctor not found with ID: {DoctorId}", updatePatientDto.DoctorId);
                    return BadRequest(new { message = "Doctor not found" });
                }

                // Check if the patient already has a history with this doctor
                var existingHistory = await _context.PatientHistories
                    .AnyAsync(ph => ph.PatientId == patient.Id && ph.DoctorId == doctor.Id);
                if (existingHistory)
                {
                    _logger.LogWarning("Patient with ID {PatientId} already has a history with doctor ID {DoctorId}", patient.Id, doctor.Id);
                    return BadRequest(new { message = "Patient already has a treatment history with this doctor" });
                }

                var patientHistory = new PatientHistory
                {
                    PatientId = patient.Id,
                    DoctorId = doctor.Id,
                    Diagnosis = updatePatientDto.Diagnosis,
                    Treatment = updatePatientDto.Treatment,
                    TreatmentDate = DateTime.UtcNow
                };
                _context.PatientHistories.Add(patientHistory);
            }

            // Update images if provided
            if (updatePatientDto.Image != null)
            {
                try
                {
                    var newImageUrl = await _imageService.SaveImageAsync(updatePatientDto.Image, "patient", id, patient.ImageUrl);
                    if (newImageUrl != null)
                    {
                        patient.ImageUrl = newImageUrl;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning("Failed to save image for patient ID {Id}: {Message}", id, ex.Message);
                    return BadRequest(new { message = ex.Message });
                }
            }

            if (updatePatientDto.XRayImage != null)
            {
                try
                {
                    var newXRayImageUrl = await _imageService.SaveImageAsync(updatePatientDto.XRayImage, "patient-xray", id, patient.XRayImageUrl);
                    if (newXRayImageUrl != null)
                    {
                        patient.XRayImageUrl = newXRayImageUrl;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning("Failed to save X-ray image for patient ID {Id}: {Message}", id, ex.Message);
                    return BadRequest(new { message = ex.Message });
                }
            }

            if (updatePatientDto.LabResultsImage != null)
            {
                try
                {
                    var newLabResultsImageUrl = await _imageService.SaveImageAsync(updatePatientDto.LabResultsImage, "patient-lab", id, patient.LabResultsImageUrl);
                    if (newLabResultsImageUrl != null)
                    {
                        patient.LabResultsImageUrl = newLabResultsImageUrl;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning("Failed to save lab results image for patient ID {Id}: {Message}", id, ex.Message);
                    return BadRequest(new { message = ex.Message });
                }
            }

            await _context.SaveChangesAsync();

            var updatedPatient = await _context.Patients
                .Include(p => p.PatientHistories)
                .ThenInclude(ph => ph.Doctor)
                .FirstOrDefaultAsync(p => p.Id == id);

            var treatmentHistories = updatedPatient!.PatientHistories.Select(ph => new TreatmentHistoryDto
            {
                Diagnosis = ph.Diagnosis,
                Treatment = ph.Treatment,
                DoctorId = ph.DoctorId,
                DoctorFirstName = ph.Doctor.FirstName,
                DoctorLastName = ph.Doctor.LastName
            }).ToList();

            var patientResponse = new PatientResponseDto
            {
                Id = updatedPatient.Id,
                FirstName = updatedPatient.FirstName,
                LastName = updatedPatient.LastName,
                Gender = updatedPatient.Gender,
                BirthDate = updatedPatient.BirthDate,
                Age = updatedPatient.Age, 
                BloodType = updatedPatient.BloodType,
                Email = updatedPatient.Email,
                ImageUrl = updatedPatient.ImageUrl,
                XRayImageUrl = updatedPatient.XRayImageUrl,
                LabResultsImageUrl = updatedPatient.LabResultsImageUrl,
                CreatedAt = updatedPatient.CreatedAt,
                Department = treatmentHistories
            };

            _logger.LogInformation("Patient updated successfully with ID: {Id}", patient.Id);
            return Ok(new { message = "Patient updated successfully", patient = patientResponse });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating patient with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while updating the patient." });
        }
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("doctor/{id}")]
    public async Task<IActionResult> DeleteDoctor(int id)
    {
        _logger.LogInformation("DeleteDoctor called for doctor ID {Id}", id);

        var doctor = await _context.Doctors.FindAsync(id);
        if (doctor == null)
        {
            _logger.LogWarning("Doctor not found with ID: {Id}", id);
            return NotFound(new { message = "Doctor not found" });
        }

        var patientHistories = await _context.PatientHistories.Where(ph => ph.DoctorId == id).ToListAsync();
        if (patientHistories.Any())
        {
            _logger.LogInformation("Deleting {Count} patient histories associated with doctor ID {Id}", patientHistories.Count, id);
            _context.PatientHistories.RemoveRange(patientHistories);
        }

        if (!string.IsNullOrEmpty(doctor.ImageUrl))
        {
            try
            {
                _imageService.DeleteImage(doctor.ImageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete image for doctor ID {Id}: {Message}", id, ex.Message);
            }
        }

        _context.Doctors.Remove(doctor);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete doctor ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the doctor." });
        }

        _logger.LogInformation("Doctor deleted successfully for ID {Id}", id);
        return Ok(new { message = "Doctor deleted successfully" });
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("patient/{id}")]
    public async Task<IActionResult> DeletePatient(int id)
    {
        _logger.LogInformation("DeletePatient called for patient ID {Id}", id);

        var patient = await _context.Patients.FindAsync(id);
        if (patient == null)
        {
            _logger.LogWarning("Patient not found with ID: {Id}", id);
            return NotFound(new { message = "Patient not found" });
        }

        var patientHistories = await _context.PatientHistories.Where(ph => ph.PatientId == id).ToListAsync();
        if (patientHistories.Any())
        {
            _logger.LogInformation("Deleting {Count} patient histories associated with patient ID {Id}", patientHistories.Count, id);
            _context.PatientHistories.RemoveRange(patientHistories);
        }

        if (!string.IsNullOrEmpty(patient.ImageUrl))
        {
            try
            {
                _imageService.DeleteImage(patient.ImageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete image for patient ID {Id}: {Message}", id, ex.Message);
            }
        }
        if (!string.IsNullOrEmpty(patient.XRayImageUrl))
        {
            try
            {
                _imageService.DeleteImage(patient.XRayImageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete X-ray image for patient ID {Id}: {Message}", id, ex.Message);
            }
        }
        if (!string.IsNullOrEmpty(patient.LabResultsImageUrl))
        {
            try
            {
                _imageService.DeleteImage(patient.LabResultsImageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete lab results image for patient ID {Id}: {Message}", id, ex.Message);
            }
        }

        _context.Patients.Remove(patient);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete patient ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the patient." });
        }

        _logger.LogInformation("Patient deleted successfully for ID {Id}", id);
        return Ok(new { message = "Patient deleted successfully" });
    }

    [Authorize(Roles = "doctor")]
    [HttpPut("patient/doctor/{patientId}")]
    public async Task<IActionResult> UpdatePatientByDoctor(int patientId, [FromForm] PatientUpdateDto updatePatientDto)
    {
        try
        {
            _logger.LogInformation("UpdatePatientByDoctor called for patient ID: {PatientId}", patientId);

            var userIdClaim = User.FindFirst("userId")?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int doctorId))
            {
                _logger.LogWarning("Invalid token claims: userId={UserId}", userIdClaim);
                return Unauthorized(new { message = "Invalid token claims" });
            }

            var patient = await _context.Patients
                .Include(p => p.PatientHistories)
                .ThenInclude(ph => ph.Doctor)
                .FirstOrDefaultAsync(p => p.Id == patientId);

            if (patient == null)
            {
                _logger.LogWarning("Patient not found with ID: {PatientId}", patientId);
                return NotFound(new { message = "Patient not found" });
            }

            var patientHistory = patient.PatientHistories.FirstOrDefault(ph => ph.DoctorId == doctorId);
            if (patientHistory == null)
            {
                _logger.LogWarning("Doctor ID {DoctorId} is not authorized to update patient ID {PatientId}", doctorId, patientId);
                return Unauthorized(new { message = "You are not authorized to update this patient." });
            }

            if (!string.IsNullOrEmpty(updatePatientDto.FirstName))
            {
                patient.FirstName = updatePatientDto.FirstName;
            }

            if (!string.IsNullOrEmpty(updatePatientDto.LastName))
            {
                patient.LastName = updatePatientDto.LastName;
            }

            if (!string.IsNullOrEmpty(updatePatientDto.Gender))
            {
                patient.Gender = updatePatientDto.Gender;
            }

            if (updatePatientDto.BirthDate.HasValue)
            {
                patient.BirthDate = updatePatientDto.BirthDate.Value;
            }

            if (!string.IsNullOrEmpty(updatePatientDto.Email))
            {
                if (_context.Patients.Any(p => p.Email == updatePatientDto.Email && p.Id != patientId))
                {
                    _logger.LogWarning("Email already exists: {Email}", updatePatientDto.Email);
                    return BadRequest(new { message = "Email already exists" });
                }
                patient.Email = updatePatientDto.Email;
            }

            if (!string.IsNullOrEmpty(updatePatientDto.Password))
            {
                patient.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updatePatientDto.Password);
            }

            if (!string.IsNullOrEmpty(updatePatientDto.Diagnosis) || !string.IsNullOrEmpty(updatePatientDto.Treatment))
            {
                if (string.IsNullOrEmpty(updatePatientDto.Diagnosis) || string.IsNullOrEmpty(updatePatientDto.Treatment))
                {
                    _logger.LogWarning("Diagnosis and Treatment must both be provided to update patient history");
                    return BadRequest(new { message = "Diagnosis and Treatment must both be provided to update patient history" });
                }

                patientHistory.Diagnosis = updatePatientDto.Diagnosis;
                patientHistory.Treatment = updatePatientDto.Treatment;
                patientHistory.TreatmentDate = DateTime.UtcNow;
            }

            if (updatePatientDto.Image != null)
            {
                try
                {
                    var newImageUrl = await _imageService.SaveImageAsync(updatePatientDto.Image, "patient", patientId, patient.ImageUrl);
                    if (newImageUrl != null)
                    {
                        patient.ImageUrl = newImageUrl;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning("Failed to save image for patient ID {PatientId}: {Message}", patientId, ex.Message);
                    return BadRequest(new { message = ex.Message });
                }
            }

            if (updatePatientDto.XRayImage != null)
            {
                try
                {
                    var newXRayImageUrl = await _imageService.SaveImageAsync(updatePatientDto.XRayImage, "patient-xray", patientId, patient.XRayImageUrl);
                    if (newXRayImageUrl != null)
                    {
                        patient.XRayImageUrl = newXRayImageUrl;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning("Failed to save X-ray image for patient ID {PatientId}: {Message}", patientId, ex.Message);
                    return BadRequest(new { message = ex.Message });
                }
            }

            if (updatePatientDto.LabResultsImage != null)
            {
                try
                {
                    var newLabResultsImageUrl = await _imageService.SaveImageAsync(updatePatientDto.LabResultsImage, "patient-lab", patientId, patient.LabResultsImageUrl);
                    if (newLabResultsImageUrl != null)
                    {
                        patient.LabResultsImageUrl = newLabResultsImageUrl;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning("Failed to save lab results image for patient ID {PatientId}: {Message}", patientId, ex.Message);
                    return BadRequest(new { message = ex.Message });
                }
            }

            await _context.SaveChangesAsync();

            var updatedPatient = await _context.Patients
                .Include(p => p.PatientHistories)
                .ThenInclude(ph => ph.Doctor)
                .FirstOrDefaultAsync(p => p.Id == patientId);

            var treatmentHistories = updatedPatient!.PatientHistories.Select(ph => new TreatmentHistoryDto
            {
                Diagnosis = ph.Diagnosis,
                Treatment = ph.Treatment,
                DoctorId = ph.DoctorId,
                DoctorFirstName = ph.Doctor.FirstName,
                DoctorLastName = ph.Doctor.LastName
            }).ToList();

            var patientResponse = new PatientResponseDto
            {
                Id = updatedPatient.Id,
                FirstName = updatedPatient.FirstName,
                LastName = updatedPatient.LastName,
                Gender = updatedPatient.Gender,
                BirthDate = updatedPatient.BirthDate,
                Age = updatedPatient.Age, 
                BloodType = updatedPatient.BloodType,
                Email = updatedPatient.Email,
                ImageUrl = updatedPatient.ImageUrl,
                XRayImageUrl = updatedPatient.XRayImageUrl,
                LabResultsImageUrl = updatedPatient.LabResultsImageUrl,
                CreatedAt = updatedPatient.CreatedAt,
                Department = treatmentHistories
            };

            _logger.LogInformation("Patient updated successfully by doctor ID {DoctorId} for patient ID {PatientId}", doctorId, patientId);
            return Ok(new { message = "Patient updated successfully", patient = patientResponse });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating patient with ID {PatientId} by doctor", patientId);
            return StatusCode(500, new { message = "An error occurred while updating the patient." });
        }
    }

    [Authorize(Roles = "doctor")]
    [HttpDelete("patient/doctor/{patientId}")]
    public async Task<IActionResult> DeletePatientByDoctor(int patientId)
    {
        try
        {
            _logger.LogInformation("DeletePatientByDoctor called for patient ID {PatientId}", patientId);

            var userIdClaim = User.FindFirst("userId")?.Value;
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int doctorId))
            {
                _logger.LogWarning("Invalid token claims: userId={UserId}", userIdClaim);
                return Unauthorized(new { message = "Invalid token claims" });
            }

            var patient = await _context.Patients
                .Include(p => p.PatientHistories)
                .FirstOrDefaultAsync(p => p.Id == patientId);

            if (patient == null)
            {
                _logger.LogWarning("Patient not found with ID: {PatientId}", patientId);
                return NotFound(new { message = "Patient not found" });
            }

            var patientHistory = patient.PatientHistories.FirstOrDefault(ph => ph.DoctorId == doctorId);
            if (patientHistory == null)
            {
                _logger.LogWarning("Doctor ID {DoctorId} is not authorized to delete patient ID {PatientId}", doctorId, patientId);
                return Unauthorized(new { message = "You are not authorized to delete this patient." });
            }

            var patientHistories = await _context.PatientHistories.Where(ph => ph.PatientId == patientId).ToListAsync();
            if (patientHistories.Any())
            {
                _logger.LogInformation("Deleting {Count} patient histories associated with patient ID {PatientId}", patientHistories.Count, patientId);
                _context.PatientHistories.RemoveRange(patientHistories);
            }

            // Delete the patient's images if they exist
            if (!string.IsNullOrEmpty(patient.ImageUrl))
            {
                try
                {
                    _imageService.DeleteImage(patient.ImageUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete image for patient ID {PatientId}: {Message}", patientId, ex.Message);
                }
            }
            if (!string.IsNullOrEmpty(patient.XRayImageUrl))
            {
                try
                {
                    _imageService.DeleteImage(patient.XRayImageUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete X-ray image for patient ID {PatientId}: {Message}", patientId, ex.Message);
                }
            }
            if (!string.IsNullOrEmpty(patient.LabResultsImageUrl))
            {
                try
                {
                    _imageService.DeleteImage(patient.LabResultsImageUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete lab results image for patient ID {PatientId}: {Message}", patientId, ex.Message);
                }
            }

            // Delete the patient
            _context.Patients.Remove(patient);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Patient deleted successfully by doctor ID {DoctorId} for patient ID {PatientId}", doctorId, patientId);
            return Ok(new { message = "Patient deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting patient with ID {PatientId} by doctor", patientId);
            return StatusCode(500, new { message = "An error occurred while deleting the patient." });
        }
    }
}