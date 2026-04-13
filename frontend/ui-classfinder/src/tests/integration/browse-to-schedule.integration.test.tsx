import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

import App from '../../App';
import { registerClass, resetMockData } from '../../api/api';
import { useAuthStore } from '../../store/authStore';
import { useScheduleStore } from '../../store/scheduleStore';

const authUser = {
  userId: 'student-123',
  role: 'student' as const,
  name: 'John Smith',
  email: 'john.smith@email.com',
};

beforeEach(() => {
  resetMockData();
  useScheduleStore.getState().reset();
  useAuthStore.setState({ user: null, initialized: false });
  localStorage.setItem('classfinder.auth.v1', JSON.stringify(authUser));
});

describe('browse to schedule integration', () => {
  test('adds class from browse and persists immediately', async () => {
    render(
      <MemoryRouter initialEntries={['/browse']}>
        <App />
      </MemoryRouter>,
    );

    const search = await screen.findByLabelText('browse');
    await userEvent.type(search, 'CSCE101');

    const enrollButton = await screen.findByRole('button', { name: /enroll csce101-01/i });
    await userEvent.click(enrollButton);

    await waitFor(() => {
      expect(localStorage.getItem('classfinder.schedules.v2')).toContain('CSCE101-01');
    });

    await userEvent.click(screen.getByRole('link', { name: /ClassFinder Scheduler/i }));

    await waitFor(() => {
      expect(screen.getAllByLabelText('CSCE101-01 09:00-10:15').length).toBeGreaterThan(0);
    });
  });

  test('removing a class persists immediately without finalize', async () => {
    await registerClass({ studentId: 'student-123', classId: 'CSCE101-01' });

    const firstRender = render(
      <MemoryRouter initialEntries={['/schedule']}>
        <App />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getAllByLabelText('CSCE101-01 09:00-10:15').length).toBeGreaterThan(0);
    });

    await userEvent.click(screen.getAllByLabelText('Remove CSCE101-01')[0]);

    await waitFor(() => {
      expect(screen.queryByLabelText('CSCE101-01 09:00-10:15')).not.toBeInTheDocument();
    });

    expect(localStorage.getItem('classfinder.schedules.v2') ?? '').not.toContain('CSCE101-01');

    firstRender.unmount();
    useScheduleStore.getState().reset();

    render(
      <MemoryRouter initialEntries={['/schedule']}>
        <App />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.queryByLabelText('CSCE101-01 09:00-10:15')).not.toBeInTheDocument();
    });
  });

  test('teacher catalog filters and enrolls directly from an instructor card', async () => {
    render(
      <MemoryRouter initialEntries={['/teachers']}>
        <App />
      </MemoryRouter>,
    );

    const search = await screen.findByLabelText('teachers');
    await userEvent.type(search, 'Brown');

    await waitFor(() => {
      expect(screen.getByRole('option', { name: /dr\. brown/i })).toBeInTheDocument();
    });

    await waitFor(() => {
      expect(screen.getByLabelText('Active filters')).toHaveTextContent('Search: Brown');
    });

    const enrollButton = await screen.findByRole('button', { name: /enroll csce331-01/i });
    await userEvent.click(enrollButton);

    await waitFor(() => {
      expect(localStorage.getItem('classfinder.schedules.v2')).toContain('CSCE331-01');
    });

    expect(await screen.findByRole('button', { name: /disenroll csce331-01/i })).toBeInTheDocument();
  });

  test('teacher workspace saves roster removals and class edits across refresh', async () => {
    localStorage.setItem(
      'classfinder.auth.v1',
      JSON.stringify({
        userId: 'teacher-2',
        role: 'teacher',
        name: 'Dr. Brown',
        email: 'brown@email.com',
      }),
    );
    useAuthStore.setState({
      user: {
        userId: 'teacher-2',
        role: 'teacher',
        name: 'Dr. Brown',
        email: 'brown@email.com',
      },
      initialized: true,
    });

    const firstRender = render(
      <MemoryRouter initialEntries={['/teachers']}>
        <App />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getByLabelText(/class title/i)).toHaveValue('Software Engineering');
    });

    const initialRemovals = screen.getAllByRole('button', { name: /^remove$/i }).length;
    await userEvent.clear(screen.getByLabelText(/class title/i));
    await userEvent.type(screen.getByLabelText(/class title/i), 'Software Engineering Studio');
    await userEvent.clear(screen.getByLabelText(/location/i));
    await userEvent.type(screen.getByLabelText(/location/i), 'ZACH 210');
    await userEvent.clear(screen.getByLabelText(/capacity/i));
    await userEvent.type(screen.getByLabelText(/capacity/i), '31');
    await userEvent.click(screen.getAllByRole('button', { name: /^remove$/i })[0]);
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));

    await waitFor(() => {
      expect(screen.getByDisplayValue('Software Engineering Studio')).toBeInTheDocument();
    });
    await waitFor(() => {
      expect(screen.getAllByRole('button', { name: /^remove$/i })).toHaveLength(initialRemovals - 1);
    });

    expect(localStorage.getItem('classfinder.rosters.v1')).toContain('CSCE331-01');

    firstRender.unmount();
  });
});
