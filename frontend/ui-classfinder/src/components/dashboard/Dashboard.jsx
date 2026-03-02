import { useEffect, useState } from "react";
import ClassCard from "./ClassCard";
import CalendarView from "./CalendarView";
import "./Dashboard.css";


function Dashboard() {

    const [studentName, setStudentName] = useState("");
    const [classes, setClasses] = useState([]);
    const [view, setView] = useState("list");
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        async function fetchDashboard() {
            try {
                const response = await fetch("http://localhost:7071/api/students/9");
                if (!response.ok) {
                    throw new Error("Failed to fetch dashboard");
                }
                const data = await response.json();
                setStudentName(`Student ${data.studentId}`);
                setClasses(data.enrolledClasses);
                setLoading(false);
            } catch (error) {
                console.error("Error fetching dashboard:", error);
                setLoading(false);
            }
        }
        fetchDashboard();
    
  }, []);

  if (loading) return <p>Loading dashboard...</p>




return (
    <div className="dashboard-container">
        <h1>Welcome, {studentName}</h1>

        <div className ="view-toggle">
            <button 
                className={view === "list" ? "active" : ""}
                onClick={() => setView("list")}
                >
                    Registered List View
                </button>

            <button
                className={view === "calendar" ? "active" : ""}
                onClick={() => setView("calendar")}
                >
                    Calendar View
                </button>
        </div>

        {view === "list" ? (
            <div className="class-list">
                {classes.map((cls) => (
                    <ClassCard key={cls.id} cls={cls} />
                ))}
            </div>
        ) : (
            <CalendarView classes={classes} />
        )}
    </div>
    );
}

export default Dashboard;