import { Navigate, Route, Routes } from 'react-router-dom';

import { Header } from './components/Header';
import { useSchedule } from './hooks/useSchedule';
import { BrowsePage } from './pages/BrowsePage';
import { SchedulePage } from './pages/SchedulePage';

export default function App(): JSX.Element {
  const { currentCredits, overlaps } = useSchedule();

  return (
    <>
      <Header currentCredits={currentCredits} hasConflicts={overlaps.length > 0} />
      <Routes>
        <Route path="/" element={<Navigate to="/schedule" replace />} />
        <Route path="/schedule" element={<SchedulePage />} />
        <Route path="/browse" element={<BrowsePage />} />
      </Routes>
    </>
  );
}
