using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Admin> Admins { get; set; }
    public DbSet<Doctor> Doctors { get; set; }
    public DbSet<Patient> Patients { get; set; }
    public DbSet<PatientHistory> PatientHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure relationships
        modelBuilder.Entity<PatientHistory>()
            .HasOne(ph => ph.Patient)
            .WithMany(p => p.PatientHistories)
            .HasForeignKey(ph => ph.PatientId);

        modelBuilder.Entity<PatientHistory>()
            .HasOne(ph => ph.Doctor)
            .WithMany(d => d.PatientHistories)
            .HasForeignKey(ph => ph.DoctorId);
    }
}