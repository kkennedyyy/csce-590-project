import { MemoryRouter } from 'react-router-dom';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import Dashboard from './Dashboard';
import * as api from '../services/api';

jest.mock('../services/api');

describe('Dashboard navigation', () => {
  test('clicking a class card opens class detail modal', async () => {
    api.getStudentClasses.mockResolvedValue([
      {
        classId: 7,
        className: 'Data Structures',
        courseCode: 'CSCE210',
        instructorName: 'Emily Anderson',
        daysTimes: 'Tue,Thu 10:30-11:45',
        location: 'ZACH 351',
        credits: 3,
        isWaitlisted: false,
        waitlistPosition: null
      }
    ]);

    api.getStudentSchedule.mockResolvedValue([
      {
        classId: 7,
        className: 'Data Structures',
        courseCode: 'CSCE210',
        dayOfWeek: 'Tue',
        startTime: '10:30',
        endTime: '11:45',
        location: 'ZACH 351'
      }
    ]);

    api.getClassDetails.mockResolvedValue({
      classId: 7,
      className: 'Data Structures',
      courseCode: 'CSCE210',
      professor: 'Emily Anderson',
      capacity: 25,
      enrolledCount: 20,
      isAtCapacity: false,
      waitlistCount: 0,
      location: 'ZACH 351',
      daysOfWeek: 'Tue,Thu',
      startTime: '10:30',
      endTime: '11:45',
      credits: 3,
      waitlistPositions: []
    });

    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <Dashboard />
      </MemoryRouter>
    );

    const classCardButton = await screen.findByRole('button', {
      name: /Open details for Data Structures/i
    });

    await user.click(classCardButton);

    const modalHeading = await screen.findByRole('heading', {
      level: 2,
      name: /Class Details/i
    });

    const dialog = modalHeading.closest('[role="dialog"]');
    expect(dialog).toBeTruthy();
    expect(within(dialog).getByText(/Emily Anderson/i)).toBeInTheDocument();
  });
});
