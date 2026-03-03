import { useEffect, useState } from 'react';
import { Link, NavLink } from 'react-router-dom';

import { SearchBar } from './SearchBar';
import styles from './Header.module.css';

interface HeaderProps {
  theme: 'dark' | 'light';
  onToggleTheme: () => void;
  showScheduleSearch?: boolean;
  scheduleSearchValue?: string;
  scheduleSuggestions?: string[];
  onScheduleSearchChange?: (value: string) => void;
}

export function Header({
  theme,
  onToggleTheme,
  showScheduleSearch = false,
  scheduleSearchValue = '',
  scheduleSuggestions,
  onScheduleSearchChange,
}: HeaderProps): JSX.Element {
  const [controlsOpen, setControlsOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);

  useEffect(() => {
    if (!showScheduleSearch) {
      setSearchOpen(false);
    }
  }, [showScheduleSearch]);

  return (
    <header className={styles.header}>
      <div className={styles.inner}>
        <div className={styles.topControls}>
          <div className={styles.controlDock}>
            <button
              type="button"
              className={styles.controlToggle}
              aria-expanded={controlsOpen}
              aria-controls="header-controls-panel"
              aria-label={controlsOpen ? 'Collapse controls menu' : 'Expand controls menu'}
              onClick={() => setControlsOpen((prev) => !prev)}
            >
              <svg
                className={styles.menuIcon}
                viewBox="0 0 24 24"
                aria-hidden="true"
                focusable="false"
              >
                <line x1="4" y1="7" x2="20" y2="7" stroke="currentColor" strokeWidth="2" />
                <line x1="4" y1="12" x2="20" y2="12" stroke="currentColor" strokeWidth="2" />
                <line x1="4" y1="17" x2="20" y2="17" stroke="currentColor" strokeWidth="2" />
              </svg>
            </button>
            {controlsOpen && (
              <div className={styles.controlsPanel} id="header-controls-panel">
                <nav aria-label="Main Navigation" className={styles.controlsNav}>
                  <NavLink to="/schedule" className={({ isActive }) => (isActive ? styles.active : '')}>
                    Schedule
                  </NavLink>
                  <NavLink to="/browse" className={({ isActive }) => (isActive ? styles.active : '')}>
                    Browse
                  </NavLink>
                  <button
                    type="button"
                    className={styles.themeToggle}
                    onClick={onToggleTheme}
                    aria-label={`Switch to ${theme === 'dark' ? 'light' : 'dark'} mode`}
                  >
                    {theme === 'dark' ? 'Light mode' : 'Dark mode'}
                  </button>
                </nav>
              </div>
            )}
          </div>

          {showScheduleSearch && onScheduleSearchChange ? (
            <div className={`${styles.controlDock} ${styles.searchDock}`}>
              <button
                type="button"
                className={styles.searchToggle}
                aria-expanded={searchOpen}
                aria-controls="header-search-panel"
                aria-label={searchOpen ? 'Collapse search panel' : 'Expand search panel'}
                onClick={() => setSearchOpen((prev) => !prev)}
              >
                <svg
                  className={styles.searchIcon}
                  viewBox="0 0 24 24"
                  aria-hidden="true"
                  focusable="false"
                >
                  <circle cx="11" cy="11" r="7" stroke="currentColor" strokeWidth="2" fill="none" />
                  <line x1="16.65" y1="16.65" x2="21" y2="21" stroke="currentColor" strokeWidth="2" />
                </svg>
              </button>
              {searchOpen && (
                <div className={styles.searchPanel} id="header-search-panel">
                  <SearchBar
                    className={styles.headerSearch}
                    label="schedule"
                    hideLabel
                    value={scheduleSearchValue}
                    onChange={onScheduleSearchChange}
                    suggestions={scheduleSuggestions}
                    placeholder="Search and press Add or drag into the schedule"
                  />
                </div>
              )}
            </div>
          ) : null}
        </div>

        <Link className={styles.title} to="/schedule">
          ClassFinder Scheduler
        </Link>
      </div>
    </header>
  );
}
