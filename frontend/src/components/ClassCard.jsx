export default function ClassCard({ classItem, onOpen }) {
  const waitlistText = classItem.isWaitlisted
    ? `Waitlisted${classItem.waitlistPosition ? ` (#${classItem.waitlistPosition})` : ''}`
    : 'Enrolled';

  return (
    <button
      type="button"
      className="class-card"
      onClick={() => onOpen(classItem.classId)}
      aria-label={`Open details for ${classItem.className}`}
    >
      <header className="card-header">
        <h3>{classItem.className}</h3>
        <span className={classItem.isWaitlisted ? 'status waitlisted' : 'status enrolled'}>
          {waitlistText}
        </span>
      </header>
      <div className="card-row">
        <span className="label">Course</span>
        <span>{classItem.courseCode}</span>
      </div>
      <div className="card-row">
        <span className="label">Instructor</span>
        <span>{classItem.instructorName}</span>
      </div>
      <div className="card-row">
        <span className="label">Days/Times</span>
        <span>{classItem.daysTimes}</span>
      </div>
      <div className="card-row">
        <span className="label">Location</span>
        <span>{classItem.location}</span>
      </div>
      <div className="card-row">
        <span className="label">Credits</span>
        <span>{classItem.credits}</span>
      </div>
    </button>
  );
}
