import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

import App from '../../App';
import { registerClass, resetMockData } from '../../api/api';
import { useScheduleStore } from '../../store/scheduleStore';

beforeEach(() => {
  resetMockData();
});

describe('browse to schedule integration', () => {
  test('adds class from browse as draft and persists only after finalize', async () => {
    render(
      <MemoryRouter initialEntries={['/browse']}>
        <App />
      </MemoryRouter>,
    );

    const search = await screen.findByLabelText('browse');
    await userEvent.type(search, 'CSCE101');

    const addButton = await screen.findByRole('button', { name: /add csce101-01 to schedule/i });
    await userEvent.click(addButton);

    await userEvent.click(screen.getByRole('link', { name: /ClassFinder Scheduler/i }));

    await waitFor(() => {
      expect(screen.getAllByLabelText('CSCE101-01 09:00-10:15').length).toBeGreaterThan(0);
    });

    const payloadBeforeFinalize = localStorage.getItem('classfinder.schedules.v2');
    expect(payloadBeforeFinalize ?? '').not.toContain('CSCE101-01');

    await userEvent.click(screen.getByRole('button', { name: /finalize registration/i }));

    await waitFor(() => {
      const payload = localStorage.getItem('classfinder.schedules.v2');
      expect(payload).toContain('CSCE101-01');
    });
  });

  test('removing a class is draft-only and does not persist without finalize', async () => {
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

    firstRender.unmount();
    useScheduleStore.getState().reset();

    render(
      <MemoryRouter initialEntries={['/schedule']}>
        <App />
      </MemoryRouter>,
    );

    await waitFor(() => {
      expect(screen.getAllByLabelText('CSCE101-01 09:00-10:15').length).toBeGreaterThan(0);
    });
  });
});
