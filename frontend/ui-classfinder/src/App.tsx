import { useEffect, useMemo, useState } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';

import { Footer } from './components/Footer';
import { Header } from './components/Header';
import { useClasses } from './hooks/useClasses';
import { AuthPage } from './pages/AuthPage';
import { BrowsePage } from './pages/BrowsePage';
import { SchedulePage } from './pages/SchedulePage';
import { SmartEnrollmentPage } from './pages/SmartEnrollmentPage';
import { TeachersPage } from './pages/TeachersPage';
import { useAuthStore } from './store/authStore';
import { useScheduleStore } from './store/scheduleStore';
import styles from './App.module.css';

const THEME_STORAGE_KEY = 'ui-classfinder.theme';

function getInitialTheme(): 'dark' | 'light' {
  if (typeof window === 'undefined') {
    return 'dark';
  }

  const saved = window.localStorage.getItem(THEME_STORAGE_KEY);
  if (saved === 'dark' || saved === 'light') {
    return saved;
  }

  const prefersLight = window.matchMedia?.('(prefers-color-scheme: light)').matches;
  return prefersLight ? 'light' : 'dark';
}

export default function App(): JSX.Element {
  const location = useLocation();
  const [theme, setTheme] = useState<'dark' | 'light'>(getInitialTheme);
  const [scheduleSearch, setScheduleSearch] = useState('');
  const { user, initialized: authInitialized, hydrate: hydrateAuth, logout } = useAuthStore();
  const setStudentId = useScheduleStore((state) => state.setStudentId);
  const resetSchedule = useScheduleStore((state) => state.reset);
  const isTeacher = user?.role === 'teacher';
  const isStudent = user?.role === 'student';
  const activeStudentId = user?.role === 'student' ? user.userId : undefined;
  const { filtered } = useClasses(scheduleSearch, '', activeStudentId);
  const homePath = isTeacher ? '/teachers' : '/schedule';

  useEffect(() => {
    if (!authInitialized) {
      hydrateAuth();
    }
  }, [authInitialized, hydrateAuth]);

  useEffect(() => {
    if (!authInitialized) {
      return;
    }

    if (activeStudentId) {
      setStudentId(activeStudentId);
      return;
    }

    resetSchedule();
  }, [activeStudentId, authInitialized, resetSchedule, setStudentId]);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    window.localStorage.setItem(THEME_STORAGE_KEY, theme);
  }, [theme]);

  const showScheduleSearch = isStudent && location.pathname === '/schedule';
  const scheduleSuggestions = useMemo(
    () => filtered.slice(0, 5).map((entry) => `${entry.item.id} ${entry.item.title}`),
    [filtered],
  );

  if (!authInitialized) {
    return <div className={styles.loadingShell}>Loading account…</div>;
  }

  return (
    <div className={styles.shell}>
      <Header
        theme={theme}
        onToggleTheme={() => setTheme((prev) => (prev === 'dark' ? 'light' : 'dark'))}
        showScheduleSearch={showScheduleSearch}
        scheduleSearchValue={scheduleSearch}
        scheduleSuggestions={showScheduleSearch ? scheduleSuggestions : undefined}
        onScheduleSearchChange={setScheduleSearch}
        user={user}
        onLogout={logout}
      />
      <div className={styles.content}>
        <Routes>
          <Route path="/" element={<Navigate to={user ? homePath : '/auth'} replace />} />
          <Route path="/auth" element={user ? <Navigate to={homePath} replace /> : <AuthPage />} />
          <Route
            path="/schedule"
            element={
              isStudent ? <SchedulePage searchTerm={scheduleSearch} /> : <Navigate to={user ? '/teachers' : '/auth'} replace />
            }
          />
          <Route
            path="/browse"
            element={isStudent ? <BrowsePage /> : <Navigate to={user ? '/teachers' : '/auth'} replace />}
          />
          <Route
            path="/smart-enrollment"
            element={isStudent ? <SmartEnrollmentPage /> : <Navigate to={user ? '/teachers' : '/auth'} replace />}
          />
          <Route path="/teachers" element={user ? <TeachersPage /> : <Navigate to="/auth" replace />} />
        </Routes>
      </div>
      <Footer />
    </div>
  );
}
