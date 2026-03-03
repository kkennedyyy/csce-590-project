import styles from './Footer.module.css';

export function Footer(): JSX.Element {
  return (
    <footer className={styles.footer} role="contentinfo">
      <div className={styles.inner}>
        <span>© {new Date().getFullYear()} ClassFinder Scheduler. All rights reserved.</span>
      </div>
    </footer>
  );
}
