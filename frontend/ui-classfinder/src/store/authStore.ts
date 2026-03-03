import { create } from 'zustand';

import type { AuthUser } from '../types';

const AUTH_STORAGE_KEY = 'classfinder.auth.v1';

interface AuthState {
  user: AuthUser | null;
  initialized: boolean;
  hydrate: () => void;
  login: (user: AuthUser) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  initialized: false,
  hydrate: () => {
    const saved = localStorage.getItem(AUTH_STORAGE_KEY);
    if (!saved) {
      set({ initialized: true });
      return;
    }

    try {
      const parsed = JSON.parse(saved) as AuthUser;
      set({ user: parsed, initialized: true });
    } catch {
      localStorage.removeItem(AUTH_STORAGE_KEY);
      set({ user: null, initialized: true });
    }
  },
  login: (user) => {
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(user));
    set({ user });
  },
  logout: () => {
    localStorage.removeItem(AUTH_STORAGE_KEY);
    set({ user: null });
  },
}));
