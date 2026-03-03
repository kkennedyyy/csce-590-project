using ClassFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassFinder.Api.Data;

public class ClassFinderDbContext(DbContextOptions<ClassFinderDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<CourseClass> CourseClasses => Set<CourseClass>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Instructor>(entity =>
        {
            entity.ToTable("Instructors");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<CourseClass>(entity =>
        {
            entity.ToTable("CourseClasses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ClassName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.CourseCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(160).IsRequired();
            entity.Property(x => x.DaysOfWeek).HasMaxLength(40).IsRequired();

            entity.HasOne(x => x.Instructor)
                .WithMany(x => x.Classes)
                .HasForeignKey(x => x.InstructorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.ToTable("Enrollments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasOne(x => x.Student)
                .WithMany(x => x.Enrollments)
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.CourseClass)
                .WithMany(x => x.Enrollments)
                .HasForeignKey(x => x.CourseClassId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.StudentId, x.CourseClassId }).IsUnique();
        });
    }
}
