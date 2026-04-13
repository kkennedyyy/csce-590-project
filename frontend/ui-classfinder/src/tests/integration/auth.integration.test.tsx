import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

import App from '../../App';
import { resetMockData } from '../../api/api';
import { useAuthStore } from '../../store/authStore';
import { useScheduleStore } from '../../store/scheduleStore';

beforeEach(() => {
  localStorage.clear();
  resetMockData();
  useScheduleStore.getState().reset();
  useAuthStore.setState({ user: null, initialized: false });
});

describe('student auth', () => {
  test('creates a student account and lands on the schedule view', async () => {
    render(
      <MemoryRouter initialEntries={['/auth']}>
        <App />
      </MemoryRouter>,
    );

    await userEvent.click(screen.getByRole('tab', { name: /create account/i }));
    await userEvent.type(screen.getByLabelText(/first name/i), 'Demo');
    await userEvent.type(screen.getByLabelText(/last name/i), 'Student');
    await userEvent.type(screen.getByLabelText(/^email$/i), 'demo.student@email.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), 'securePass123');
    await userEvent.click(screen.getByRole('button', { name: /create account/i }));

    await waitFor(() => {
      expect(screen.getByRole('status', { name: /current credits/i })).toHaveTextContent(
        'Current credits: 0 / 19',
      );
    });

    expect(localStorage.getItem('classfinder.auth.v1')).toContain('demo.student@email.com');
  });

  test('teacher login lands on the teaching workspace', async () => {
    render(
      <MemoryRouter initialEntries={['/auth']}>
        <App />
      </MemoryRouter>,
    );

    await userEvent.click(screen.getByRole('tab', { name: /teacher/i }));
    await userEvent.click(screen.getByRole('button', { name: /use demo teacher/i }));
    await userEvent.click(screen.getByRole('button', { name: /^sign in$/i }));

    await waitFor(() => {
      expect(screen.getByText(/teacher workspace/i)).toBeInTheDocument();
    });

    expect(screen.getByRole('heading', { name: /your classes/i })).toBeInTheDocument();
    expect(localStorage.getItem('classfinder.auth.v1')).toContain('brown@email.com');
  });
});
