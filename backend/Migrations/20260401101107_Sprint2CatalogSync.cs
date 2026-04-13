using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassFinder.Api.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2CatalogSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_CourseClassId",
                table: "Enrollments");

            migrationBuilder.AddColumn<string>(
                name: "Classification",
                table: "Students",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Students",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Major",
                table: "Students",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "Students",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Instructors",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "Instructors",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalRecordId",
                table: "Enrollments",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSeenInExternalSyncUtc",
                table: "Enrollments",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSystem",
                table: "Enrollments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StatusChangedAtUtc",
                table: "Enrollments",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "CourseNumber",
                table: "CourseClasses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "CourseClasses",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DepartmentCode",
                table: "CourseClasses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "CourseClasses",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DropDeadlineUtc",
                table: "CourseClasses",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "CourseClasses",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Semester",
                table: "CourseClasses",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SessionCode",
                table: "CourseClasses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CoursePrerequisites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseClassId = table.Column<int>(type: "int", nullable: false),
                    RequiredCourseCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoursePrerequisites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoursePrerequisites_CourseClasses_CourseClassId",
                        column: x => x.CourseClassId,
                        principalTable: "CourseClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentCourseHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudentId = table.Column<int>(type: "int", nullable: false),
                    CourseCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCourseHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentCourseHistories_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_ExternalId",
                table: "Students",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Instructors_ExternalId",
                table: "Instructors",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_CourseClassId_WaitlistPosition",
                table: "Enrollments",
                columns: new[] { "CourseClassId", "WaitlistPosition" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_ExternalRecordId",
                table: "Enrollments",
                column: "ExternalRecordId",
                filter: "[ExternalRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CourseClasses_DepartmentCode",
                table: "CourseClasses",
                column: "DepartmentCode");

            migrationBuilder.CreateIndex(
                name: "IX_CourseClasses_ExternalId",
                table: "CourseClasses",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CoursePrerequisites_CourseClassId_RequiredCourseCode",
                table: "CoursePrerequisites",
                columns: new[] { "CourseClassId", "RequiredCourseCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentCourseHistories_StudentId_CourseCode",
                table: "StudentCourseHistories",
                columns: new[] { "StudentId", "CourseCode" },
                unique: true);

            ApplyEmbeddedSqlScript(migrationBuilder, "ClassFinder.Api.Sql.Bootstrap.09_Sprint2CatalogSync.sql");
            ApplyEmbeddedSqlScript(migrationBuilder, "ClassFinder.Api.Sql.Bootstrap.10_ExternalSyncStaging.sql");
            ApplyEmbeddedSqlScript(migrationBuilder, "ClassFinder.Api.Sql.Bootstrap.10_StoredProcedure.sql");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoursePrerequisites");

            migrationBuilder.DropTable(
                name: "StudentCourseHistories");

            migrationBuilder.DropIndex(
                name: "IX_Students_ExternalId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Instructors_ExternalId",
                table: "Instructors");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_CourseClassId_WaitlistPosition",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_ExternalRecordId",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_CourseClasses_DepartmentCode",
                table: "CourseClasses");

            migrationBuilder.DropIndex(
                name: "IX_CourseClasses_ExternalId",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "Classification",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Major",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Instructors");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "Instructors");

            migrationBuilder.DropColumn(
                name: "ExternalRecordId",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "LastSeenInExternalSyncUtc",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "SourceSystem",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "StatusChangedAtUtc",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "CourseNumber",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "DepartmentCode",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "DropDeadlineUtc",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "Semester",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "SessionCode",
                table: "CourseClasses");

            migrationBuilder.Sql(
                """
                IF OBJECT_ID('dbo.usp_ClassFinder_ApplyExternalSync', 'P') IS NOT NULL
                    DROP PROCEDURE dbo.usp_ClassFinder_ApplyExternalSync;
                IF OBJECT_ID('dbo.usp_ClassFinder_FailExternalSync', 'P') IS NOT NULL
                    DROP PROCEDURE dbo.usp_ClassFinder_FailExternalSync;
                IF OBJECT_ID('dbo.usp_ClassFinder_BeginExternalSync', 'P') IS NOT NULL
                    DROP PROCEDURE dbo.usp_ClassFinder_BeginExternalSync;
                IF OBJECT_ID('dbo.ufn_ClassFinder_NormalizeDays', 'FN') IS NOT NULL
                    DROP FUNCTION dbo.ufn_ClassFinder_NormalizeDays;
                IF OBJECT_ID('dbo.StageClassFinderWaitlist', 'U') IS NOT NULL
                    DROP TABLE dbo.StageClassFinderWaitlist;
                IF OBJECT_ID('dbo.StageClassFinderEnrollments', 'U') IS NOT NULL
                    DROP TABLE dbo.StageClassFinderEnrollments;
                IF OBJECT_ID('dbo.StageClassFinderClasses', 'U') IS NOT NULL
                    DROP TABLE dbo.StageClassFinderClasses;
                IF OBJECT_ID('dbo.StageClassFinderProfessors', 'U') IS NOT NULL
                    DROP TABLE dbo.StageClassFinderProfessors;
                IF OBJECT_ID('dbo.StageClassFinderStudents', 'U') IS NOT NULL
                    DROP TABLE dbo.StageClassFinderStudents;
                IF OBJECT_ID('dbo.ExternalSourceSyncRuns', 'U') IS NOT NULL
                    DROP TABLE dbo.ExternalSourceSyncRuns;
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_CourseClassId",
                table: "Enrollments",
                column: "CourseClassId");
        }

        private static void ApplyEmbeddedSqlScript(MigrationBuilder migrationBuilder, string resourceName)
        {
            var assembly = typeof(Sprint2CatalogSync).Assembly;
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded SQL resource '{resourceName}' was not found.");
            using var reader = new StreamReader(stream);
            var script = reader.ReadToEnd();

            foreach (
                var batch in Regex.Split(
                    script,
                    @"^\s*GO\s*$",
                    RegexOptions.Multiline | RegexOptions.IgnoreCase
                )
            )
            {
                var sql = batch.Trim();
                if (sql.Length > 0)
                {
                    migrationBuilder.Sql(sql);
                }
            }
        }
    }
}
