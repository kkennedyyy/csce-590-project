import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

import App from '../../App';
import { resetMockData } from '../../api/api';

beforeEach(() => {
  resetMockData();
});

describe('browse to schedule integration', () => {
  test('adds class from browse and persists schedule to localStorage', async () => {
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

    await act(async () => {
      const payload = localStorage.getItem('classfinder.schedules.v2');
      expect(payload).toContain('CSCE101-01');
    });
  });
});
