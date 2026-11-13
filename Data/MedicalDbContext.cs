using MedicalApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MedicalApi.Data;

public class MedicalDbContext : DbContext
{
    public MedicalDbContext(DbContextOptions<MedicalDbContext> options) : base(options)
    {
    }

    public DbSet<Patient> Patients { get; set; }
    public DbSet<Spravka> Spravki { get; set; }
    public DbSet<SpravkaType> SpravkaTypes { get; set; }
    public DbSet<Appointment> Appointments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Пациент
        modelBuilder.Entity<Patient>(entity =>
        {
            entity.Property(p => p.Name).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Iin).IsRequired().HasMaxLength(12);
            entity.Property(p => p.Phone).IsRequired().HasMaxLength(20);
            entity.Property(p => p.Address).HasMaxLength(200);
            entity.Property(p => p.Email).HasMaxLength(100);

            // ИИН уникален: база сама не даст завести одного человека дважды
            entity.HasIndex(p => p.Iin).IsUnique();
        });

        // Вид справки: цена в каталоге не может быть нулевой или отрицательной
        modelBuilder.Entity<SpravkaType>(entity =>
        {
            entity.Property(t => t.Name).IsRequired().HasMaxLength(100);
            entity.Property(t => t.Price).HasPrecision(10, 2);

            entity.ToTable(t => t.HasCheckConstraint("CK_SpravkaTypes_Price", "\"Price\" > 0"));
        });

        // Справка
        modelBuilder.Entity<Spravka>(entity =>
        {
            entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
            entity.Property(s => s.Price).HasPrecision(10, 2);

            // У справки один пациент, у пациента много справок, связь через PatientId.
            // Restrict: пациента со справками база удалить не даст, это медицинская история
            entity.HasOne<Patient>()
                .WithMany()
                .HasForeignKey(s => s.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            // У справки один вид, у вида много справок, связь через SpravkaTypeId
            entity.HasOne<SpravkaType>()
                .WithMany()
                .HasForeignKey(s => s.SpravkaTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Запись на приём
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.Property(a => a.DoctorName).IsRequired().HasMaxLength(100);
            entity.Property(a => a.Specialty).IsRequired().HasMaxLength(50);
            entity.Property(a => a.Complaint).HasMaxLength(500);

            // Время приёма — настенное время клиники, без часового пояса.
            // С timestamptz Npgsql требует только UTC и падает на "2026-11-20T09:30"
            entity.Property(a => a.Time).HasColumnType("timestamp without time zone");

            // Пациента с записями на приём тоже нельзя удалить молча
            entity.HasOne<Patient>()
                .WithMany()
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
