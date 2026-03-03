import { useEffect, useMemo, useState } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';

import { Footer } from './components/Footer';
import { Header } from './components/Header';
import { useClasses } from './hooks/useClasses';
import { BrowsePage } from './pages/BrowsePage';
import { SchedulePage } from './pages/SchedulePage';
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
  const { filtered } = useClasses(scheduleSearch);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    window.localStorage.setItem(THEME_STORAGE_KEY, theme);
  }, [theme]);

  const showScheduleSearch = location.pathname === '/schedule';
  const scheduleSuggestions = useMemo(
    () => filtered.slice(0, 5).map((entry) => `${entry.item.id} ${entry.item.title}`),
    [filtered],
  );

  return (
    <div className={styles.shell}>
      <Header
        theme={theme}
        onToggleTheme={() => setTheme((prev) => (prev === 'dark' ? 'light' : 'dark'))}
        showScheduleSearch={showScheduleSearch}
        scheduleSearchValue={scheduleSearch}
        scheduleSuggestions={showScheduleSearch ? scheduleSuggestions : undefined}
        onScheduleSearchChange={setScheduleSearch}
      />
      <div className={styles.content}>
        <Routes>
          <Route path="/" element={<Navigate to="/schedule" replace />} />
          <Route path="/schedule" element={<SchedulePage searchTerm={scheduleSearch} />} />
          <Route path="/browse" element={<BrowsePage />} />
        </Routes>
      </div>
      <Footer />
    </div>
  );
}
