using AuthApi.Data;
using AuthApi.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthApi.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(AppDbContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<DoctorResponseDto>> GetAllDoctorsAsync(int pageNumber, int pageSize, string? searchTerm)
    {
        try
        {
            _logger.LogInformation("Fetching all doctors with pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}", pageNumber, pageSize, searchTerm);

            var query = _context.Doctors.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(d => d.FirstName.ToLower().Contains(searchTerm) ||
                                         d.LastName.ToLower().Contains(searchTerm) ||
                                         d.Email.ToLower().Contains(searchTerm) ||
                                         d.Specialty.ToLower().Contains(searchTerm));
            }

            var totalCount = await query.CountAsync();
            var doctors = await query
                .OrderBy(d => d.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new DoctorResponseDto
                {
                    Id = d.Id,
                    FirstName = d.FirstName,
                    LastName = d.LastName,
                    Gender = d.Gender,
                    BirthDate = d.BirthDate,
                    Specialty = d.Specialty,
                    Email = d.Email,
                    ImageUrl = d.ImageUrl
                })
                .ToListAsync();

            return new PagedResult<DoctorResponseDto>
            {
                Data = doctors,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching doctors. StackTrace: {StackTrace}", ex.StackTrace);
            throw;
        }
    }

    public async Task<PagedResult<PatientResponseDto>> GetAllPatientsAsync(int pageNumber, int pageSize, string? searchTerm)
    {
        try
        {
            _logger.LogInformation("Fetching all patients with pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}", pageNumber, pageSize, searchTerm);

            var query = _context.Patients
                .Include(p => p.PatientHistories)
                .ThenInclude(ph => ph.Doctor)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(p => p.FirstName.ToLower().Contains(searchTerm) ||
                                         p.LastName.ToLower().Contains(searchTerm) ||
                                         p.Email.ToLower().Contains(searchTerm));
            }

            var totalCount = await query.CountAsync();
            var patients = await query
                .OrderBy(p => p.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PatientResponseDto
                {
                    Id = p.Id,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    Gender = p.Gender,
                    BirthDate = p.BirthDate,
                    Email = p.Email,
                    ImageUrl = p.ImageUrl,
                    CreatedAt = p.CreatedAt,
                    Department = p.PatientHistories.Select(ph => new TreatmentHistoryDto
                    {
                        Diagnosis = ph.Diagnosis,
                        Treatment = ph.Treatment,
                        DoctorId = ph.DoctorId,
                        DoctorFirstName = ph.Doctor.FirstName,
                        DoctorLastName = ph.Doctor.LastName
                    }).ToList()
                })
                .ToListAsync();

            return new PagedResult<PatientResponseDto>
            {
                Data = patients,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching patients. StackTrace: {StackTrace}", ex.StackTrace);
            throw;
        }
    }

    public async Task<PagedResult<PatientResponseDto>> GetPatientsByDoctorAsync(int doctorId, int pageNumber, int pageSize, string? searchTerm)
    {
        try
        {
            _logger.LogInformation("Fetching patients for doctorId: {DoctorId}, pageNumber: {PageNumber}, pageSize: {PageSize}, searchTerm: {SearchTerm}", doctorId, pageNumber, pageSize, searchTerm);

            var doctorExists = await _context.Doctors.AnyAsync(d => d.Id == doctorId);
            if (!doctorExists)
            {
                _logger.LogWarning("Doctor not found: {DoctorId}", doctorId);
                throw new InvalidOperationException("Doctor not found");
            }

            var query = _context.Patients
                .Include(p => p.PatientHistories)
                .ThenInclude(ph => ph.Doctor)
                .Where(p => p.PatientHistories.Any(ph => ph.DoctorId == doctorId))
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(p => p.FirstName.ToLower().Contains(searchTerm) ||
                                         p.LastName.ToLower().Contains(searchTerm) ||
                                         p.Email.ToLower().Contains(searchTerm));
            }

            var totalCount = await query.CountAsync();
            var patients = await query
                .OrderBy(p => p.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PatientResponseDto
                {
                    Id = p.Id,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    Gender = p.Gender,
                    BirthDate = p.BirthDate,
                    Email = p.Email,
                    ImageUrl = p.ImageUrl,
                    CreatedAt = p.CreatedAt,
                    Department = p.PatientHistories
                        .Where(ph => ph.DoctorId == doctorId)
                        .Select(ph => new TreatmentHistoryDto
                        {
                            Diagnosis = ph.Diagnosis,
                            Treatment = ph.Treatment,
                            DoctorId = ph.DoctorId,
                            DoctorFirstName = ph.Doctor.FirstName,
                            DoctorLastName = ph.Doctor.LastName
                        }).ToList()
                })
                .ToListAsync();

            return new PagedResult<PatientResponseDto>
            {
                Data = patients,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching patients for doctorId: {DoctorId}. StackTrace: {StackTrace}", doctorId, ex.StackTrace);
            throw;
        }
    }

    public async Task<object?> GetUserDetailsAsync(int userId, string role)
    {
        try
        {
            _logger.LogInformation("Fetching user details for userId: {UserId}, role: {Role}", userId, role);

            switch (role.ToLower())
            {
                case "admin":
                    var admin = await _context.Admins
                        .Where(a => a.Id == userId)
                        .Select(a => new AdminResponseDto
                        {
                            Id = a.Id,
                            Email = a.Email
                        })
                        .FirstOrDefaultAsync();
                    return admin;

                case "doctor":
                    var doctor = await _context.Doctors
                        .Where(d => d.Id == userId)
                        .Select(d => new DoctorResponseDto
                        {
                            Id = d.Id,
                            FirstName = d.FirstName,
                            LastName = d.LastName,
                            Gender = d.Gender,
                            BirthDate = d.BirthDate,
                            Specialty = d.Specialty,
                            Email = d.Email,
                            ImageUrl = d.ImageUrl
                        })
                        .FirstOrDefaultAsync();
                    return doctor;

                case "patient":
                    var patient = await _context.Patients
                        .Include(p => p.PatientHistories)
                        .ThenInclude(ph => ph.Doctor)
                        .Where(p => p.Id == userId)
                        .Select(p => new PatientResponseDto
                        {
                            Id = p.Id,
                            FirstName = p.FirstName,
                            LastName = p.LastName,
                            Gender = p.Gender,
                            BirthDate = p.BirthDate,
                            Email = p.Email,
                            ImageUrl = p.ImageUrl,
                            CreatedAt = p.CreatedAt,
                            Department = p.PatientHistories.Select(ph => new TreatmentHistoryDto
                            {
                                Diagnosis = ph.Diagnosis,
                                Treatment = ph.Treatment,
                                DoctorId = ph.DoctorId,
                                DoctorFirstName = ph.Doctor.FirstName,
                                DoctorLastName = ph.Doctor.LastName
                            }).ToList()
                        })
                        .FirstOrDefaultAsync();
                    return patient;

                default:
                    _logger.LogWarning("Invalid role: {Role}", role);
                    return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user details for userId: {UserId}, role: {Role}. StackTrace: {StackTrace}", userId, role, ex.StackTrace);
            throw;
        }
    }
}