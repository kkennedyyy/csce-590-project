import { MemoryRouter } from 'react-router-dom';
import { render, screen } from '@testing-library/react';
import Dashboard from './Dashboard';
import * as api from '../services/api';

jest.mock('../services/api');

describe('Dashboard', () => {
  test('matches snapshot when classes render', async () => {
    api.getStudentClasses.mockResolvedValue([
      {
        classId: 1,
        className: 'Introduction to Computer Science',
        courseCode: 'CSCE101',
        instructorName: 'Emily Anderson',
        daysTimes: 'Mon,Wed 09:00-10:15',
        location: 'ENGR 205',
        credits: 3,
        isWaitlisted: false,
        waitlistPosition: null
      }
    ]);

    api.getStudentSchedule.mockResolvedValue([
      {
        classId: 1,
        className: 'Introduction to Computer Science',
        courseCode: 'CSCE101',
        dayOfWeek: 'Mon',
        startTime: '09:00',
        endTime: '10:15',
        location: 'ENGR 205'
      }
    ]);

    const { container } = render(
      <MemoryRouter>
        <Dashboard />
      </MemoryRouter>
    );

    await screen.findByText(/Introduction to Computer Science/i);

    expect(container.querySelector('.class-card')).toMatchInlineSnapshot(`
<button
  aria-label="Open details for Introduction to Computer Science"
  class="class-card"
  type="button"
>
  <header
    class="card-header"
  >
    <h3>
      Introduction to Computer Science
    </h3>
    <span
      class="status enrolled"
    >
      Enrolled
    </span>
  </header>
  <div
    class="card-row"
  >
    <span
      class="label"
    >
      Course
    </span>
    <span>
      CSCE101
    </span>
  </div>
  <div
    class="card-row"
  >
    <span
      class="label"
    >
      Instructor
    </span>
    <span>
      Emily Anderson
    </span>
  </div>
  <div
    class="card-row"
  >
    <span
      class="label"
    >
      Days/Times
    </span>
    <span>
      Mon,Wed 09:00-10:15
    </span>
  </div>
  <div
    class="card-row"
  >
    <span
      class="label"
    >
      Location
    </span>
    <span>
      ENGR 205
    </span>
  </div>
  <div
    class="card-row"
  >
    <span
      class="label"
    >
      Credits
    </span>
    <span>
      3
    </span>
  </div>
</button>
`);
  });
});
