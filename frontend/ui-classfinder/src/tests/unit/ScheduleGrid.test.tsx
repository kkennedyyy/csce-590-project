import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ScheduleGrid } from '../../components/ScheduleGrid';
import type { ScheduledClass } from '../../types';
import { getOverlaps } from '../../utils/validators';

const schedule: ScheduledClass[] = [
  {
    classId: 'CSCE101-01',
    title: 'Intro to CS',
    instructor: 'Dr. Smith',
    credits: 3,
    room: 'A',
    term: 'Fall 2026',
    days: ['Mon'],
    startTime: '09:00',
    endTime: '10:00',
  },
  {
    classId: 'MATH200-01',
    title: 'Calc II',
    instructor: 'Prof. Adams',
    credits: 4,
    room: 'B',
    term: 'Fall 2026',
    days: ['Mon'],
    startTime: '09:30',
    endTime: '10:30',
  },
];

describe('ScheduleGrid', () => {
  test('renders scheduled class tiles', () => {
    render(
      <ScheduleGrid
        schedule={[schedule[0]]}
        overlaps={[]}
        onRemoveClass={async () => {}}
        onKeyboardAdd={async () => {}}
      />,
    );

    expect(screen.getByLabelText('CSCE101-01 09:00-10:00')).toBeInTheDocument();
  });

  test('shows conflict overlay when schedule has overlap', async () => {
    render(
      <ScheduleGrid
        schedule={schedule}
        overlaps={getOverlaps(schedule)}
        onRemoveClass={async () => {}}
        onKeyboardAdd={async () => {}}
      />,
    );

    expect(screen.getByTestId('conflict-overlay')).toBeInTheDocument();

    await userEvent.click(screen.getByLabelText('Remove CSCE101-01'));
  });
});
