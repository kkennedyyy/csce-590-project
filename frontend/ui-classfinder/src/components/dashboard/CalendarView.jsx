import "./CalendarView.css"

function CalendarView({ classes }) {

    const weekDays = ["Mon", "Tue", "Wed", "Thu", "Fri"];
    const groupedByDay = {};

    weekDays.forEach(day => {
        groupedByDay[day] = [];
    });

    classes.forEach((cls) => {
    const [daysPart, ...timePart] = cls.schedule.split(" ");
    const time = timePart.join(" ");
    const days = daysPart.split("/");

    days.forEach((day) => {
      if (groupedByDay[day]) {
        groupedByDay[day].push({
            courseCode: cls.courseCode,
            className: cls.className,
            time
        });
      }
      });
    });

  return (
    <div className="calendar-view-container">
      <h2>Weekly Schedule</h2>
      
      <div className ="calendar-grid">
        {weekDays.map((day) => (
            <div key ={day} className="calendar-column">
                <div className="calendar-header">{day}</div>
    

          {groupedByDay[day].map((item, index) => (
            <div key={index} className="calendar-card">
                <div className="calendar-time">{item.time}</div>
                <div className="calendar-course">{item.courseCode}</div>
            </div>
          ))}
        </div>
      ))}
    </div>
    </div>
  );
}

export default CalendarView;