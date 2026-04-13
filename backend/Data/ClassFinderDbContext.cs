using ClassFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassFinder.Api.Data;

public class ClassFinderDbContext(DbContextOptions<ClassFinderDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<CourseClass> CourseClasses => Set<CourseClass>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<CoursePrerequisite> CoursePrerequisites => Set<CoursePrerequisite>();
    public DbSet<StudentCourseHistory> StudentCourseHistories => Set<StudentCourseHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("Students");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalId).HasMaxLength(128);
            entity.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Password).HasMaxLength(200);
            entity.Property(x => x.Major).HasMaxLength(120);
            entity.Property(x => x.Classification).HasMaxLength(60);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.ExternalId).IsUnique().HasFilter("[ExternalId] IS NOT NULL");
        });

        modelBuilder.Entity<Instructor>(entity =>
        {
            entity.ToTable("Instructors");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalId).HasMaxLength(128);
            entity.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Password).HasMaxLength(200);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.ExternalId).IsUnique().HasFilter("[ExternalId] IS NOT NULL");
        });

        modelBuilder.Entity<CourseClass>(entity =>
        {
            entity.ToTable("CourseClasses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalId).HasMaxLength(128);
            entity.Property(x => x.ClassName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.CourseCode).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Department).HasMaxLength(120);
            entity.Property(x => x.DepartmentCode).HasMaxLength(20);
            entity.Property(x => x.SessionCode).HasMaxLength(20);
            entity.Property(x => x.Semester).HasMaxLength(40);
            entity.Property(x => x.Location).HasMaxLength(160).IsRequired();
            entity.Property(x => x.DaysOfWeek).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => x.ExternalId).IsUnique().HasFilter("[ExternalId] IS NOT NULL");
            entity.HasIndex(x => x.DepartmentCode);

            entity.HasOne(x => x.Instructor)
                .WithMany(x => x.Classes)
                .HasForeignKey(x => x.InstructorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.ToTable("Enrollments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalRecordId).HasMaxLength(128);
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.SourceSystem).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.ExternalRecordId).HasFilter("[ExternalRecordId] IS NOT NULL");

            entity.HasOne(x => x.Student)
                .WithMany(x => x.Enrollments)
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.CourseClass)
                .WithMany(x => x.Enrollments)
                .HasForeignKey(x => x.CourseClassId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.StudentId, x.CourseClassId }).IsUnique();
            entity.HasIndex(x => new { x.CourseClassId, x.WaitlistPosition });
        });

        modelBuilder.Entity<CoursePrerequisite>(entity =>
        {
            entity.ToTable("CoursePrerequisites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequiredCourseCode).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.CourseClassId, x.RequiredCourseCode }).IsUnique();

            entity.HasOne(x => x.CourseClass)
                .WithMany(x => x.Prerequisites)
                .HasForeignKey(x => x.CourseClassId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StudentCourseHistory>(entity =>
        {
            entity.ToTable("StudentCourseHistories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CourseCode).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.StudentId, x.CourseCode }).IsUnique();

            entity.HasOne(x => x.Student)
                .WithMany(x => x.CompletedCourses)
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
