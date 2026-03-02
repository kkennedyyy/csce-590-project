import { useParams } from "react-router-dom";
import { useEffect, useState } from "react";
import "./ClassDetail.css";

function ClassDetail() {
  const { id } = useParams();
  const [details, setDetails] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function fetchDetail() {
      try {
        const response = await fetch(
          `http://localhost:7071/api/classes/${id}`
        );

        if (!response.ok) {
          throw new Error("Failed to fetch class details");
        }

        const data = await response.json();
        setDetails(data);
        setLoading(false);
      } catch (error) {
        console.error("Error fetching class details:", error);
        setLoading(false);
      }
    }

    fetchDetail();
  }, [id]);

  if (loading) return <p>Loading class details...</p>;

  if (!details) return <p>Class not found.</p>;

  return (
    <div className="class-detail-container">
      <h1>
        {details.courseCode} - {details.courseName}
      </h1>

      <p><strong>Instructor:</strong> {details.instructor}</p>
      <p><strong>Credits:</strong> {details.credits}</p>
      <p><strong>Capacity:</strong> {details.capacity}</p>

      <h3>Schedule</h3>
      {details.schedule.length === 0 ? (
        <p>TBA</p>
      ) : (
        details.schedule.map((s, index) => (
          <div key={index}>
            <p>
              {s.day} {s.startTime} - {s.endTime}
            </p>
            <p>{s.location}</p>
          </div>
        ))
      )}
    </div>
  );
}

export default ClassDetail;