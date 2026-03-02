import { useNavigate } from "react-router-dom";

function ClassCard({ cls }) {
  const navigate = useNavigate();

  return (
    <div
      className="class-card"
      onClick={() => navigate(`/class/${cls.id}`)}
      style={{ cursor: "pointer" }}
    >
      <h2>
        {cls.courseCode} - {cls.className}
      </h2>

      <p><strong>Instructor:</strong> {cls.instructor}</p>
      <p><strong>Schedule:</strong> {cls.schedule}</p>
      <p><strong>Location:</strong> {cls.location}</p>
      <p><strong>Credits:</strong> {cls.credits}</p>

      <p className="status">Enrolled</p>
    </div>
  );
}

export default ClassCard;