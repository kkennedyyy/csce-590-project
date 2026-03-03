import { useEffect, useState } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';

import { Footer } from './components/Footer';
import { Header } from './components/Header';
import { useSchedule } from './hooks/useSchedule';
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
  const { currentCredits, overlaps } = useSchedule();
  const [theme, setTheme] = useState<'dark' | 'light'>(getInitialTheme);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    window.localStorage.setItem(THEME_STORAGE_KEY, theme);
  }, [theme]);

  return (
    <div className={styles.shell}>
      <Header
        currentCredits={currentCredits}
        hasConflicts={overlaps.length > 0}
        theme={theme}
        onToggleTheme={() => setTheme((prev) => (prev === 'dark' ? 'light' : 'dark'))}
      />
      <div className={styles.content}>
        <Routes>
          <Route path="/" element={<Navigate to="/schedule" replace />} />
          <Route path="/schedule" element={<SchedulePage />} />
          <Route path="/browse" element={<BrowsePage />} />
        </Routes>
      </div>
      <Footer />
    </div>
  );
}
