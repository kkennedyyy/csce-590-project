const DAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri'];
const START_MINUTES = 8 * 60;
const END_MINUTES = 20 * 60;
const PIXELS_PER_MINUTE = 1;

function toMinutes(time) {
  const [hour, minute] = time.split(':').map(Number);
  return hour * 60 + minute;
}

function formatHour(hour24) {
  const suffix = hour24 >= 12 ? 'PM' : 'AM';
  const hour12 = hour24 % 12 === 0 ? 12 : hour24 % 12;
  return `${hour12}:00 ${suffix}`;
}

export default function CalendarView({ events, onSelectClass }) {
  const hours = Array.from({ length: END_MINUTES / 60 - START_MINUTES / 60 + 1 }, (_, index) =>
    START_MINUTES / 60 + index
  );

  return (
    <section className="calendar" aria-label="Student class calendar view">
      <div className="time-rail" aria-hidden="true">
        {hours.map((hour) => (
          <div key={hour} className="time-marker" style={{ top: `${(hour * 60 - START_MINUTES) * PIXELS_PER_MINUTE}px` }}>
            {formatHour(hour)}
          </div>
        ))}
      </div>
      <div className="calendar-grid" role="table" aria-label="Weekly class schedule">
        {DAYS.map((day) => (
          <div key={day} className="day-column" role="columnheader">
            <div className="day-title">{day}</div>
            <div className="day-track">
              {events
                .filter((event) => event.dayOfWeek === day)
                .map((event) => {
                  const top = (toMinutes(event.startTime) - START_MINUTES) * PIXELS_PER_MINUTE;
                  const height = Math.max(
                    30,
                    (toMinutes(event.endTime) - toMinutes(event.startTime)) * PIXELS_PER_MINUTE
                  );
                  return (
                    <button
                      key={`${event.classId}-${event.dayOfWeek}-${event.startTime}`}
                      type="button"
                      className="calendar-event"
                      style={{ top: `${top}px`, height: `${height}px` }}
                      onClick={() => onSelectClass(event.classId)}
                      aria-label={`${event.className} ${event.dayOfWeek} ${event.startTime} to ${event.endTime}`}
                    >
                      <strong>{event.courseCode}</strong>
                      <span>{event.startTime} - {event.endTime}</span>
                      <span>{event.location}</span>
                    </button>
                  );
                })}
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
