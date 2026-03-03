import { useMemo, useState } from 'react';

import styles from './SearchBar.module.css';

interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  suggestions?: string[];
  label: string;
  hideLabel?: boolean;
  className?: string;
}

export function SearchBar({
  value,
  onChange,
  placeholder = 'Search classes, instructor, or ID',
  suggestions,
  label,
  hideLabel = false,
  className,
}: SearchBarProps): JSX.Element {
  const [activeIndex, setActiveIndex] = useState<number>(-1);
  const [isOpen, setIsOpen] = useState<boolean>(false);

  const availableSuggestions = useMemo(() => suggestions?.slice(0, 5) ?? [], [suggestions]);

  return (
    <div className={`${styles.wrapper} ${className ?? ''}`}>
      <label className={`${styles.label} ${hideLabel ? styles.srOnly : ''}`} htmlFor={`${label}-search`}>
        {label}
      </label>
      <input
        id={`${label}-search`}
        className={styles.input}
        type="text"
        role="combobox"
        aria-expanded={availableSuggestions.length > 0}
        aria-controls={`${label}-search-list`}
        aria-activedescendant={
          activeIndex >= 0 ? `${label}-search-option-${activeIndex}` : undefined
        }
        value={value}
        placeholder={placeholder}
        onFocus={() => setIsOpen(true)}
        onBlur={() => {
          window.setTimeout(() => setIsOpen(false), 100);
        }}
        onChange={(event) => {
          setActiveIndex(-1);
          setIsOpen(true);
          onChange(event.target.value);
        }}
        onKeyDown={(event) => {
          if (!availableSuggestions.length) {
            return;
          }

          if (event.key === 'ArrowDown') {
            event.preventDefault();
            setActiveIndex((prev) => (prev + 1) % availableSuggestions.length);
          }

          if (event.key === 'ArrowUp') {
            event.preventDefault();
            setActiveIndex((prev) =>
              prev <= 0 ? availableSuggestions.length - 1 : Math.max(0, prev - 1),
            );
          }

          if (event.key === 'Enter' && activeIndex >= 0) {
            event.preventDefault();
            setIsOpen(false);
            onChange(availableSuggestions[activeIndex]);
          }

          if (event.key === 'Escape') {
            setIsOpen(false);
          }
        }}
      />
      {isOpen && availableSuggestions.length > 0 && (
        <ul id={`${label}-search-list`} className={styles.suggestions} role="listbox">
          {availableSuggestions.map((suggestion, index) => (
            <li
              id={`${label}-search-option-${index}`}
              key={suggestion}
              className={index === activeIndex ? styles.active : ''}
              role="option"
              aria-selected={index === activeIndex}
              onMouseDown={() => {
                setIsOpen(false);
                onChange(suggestion);
              }}
            >
              {suggestion}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
