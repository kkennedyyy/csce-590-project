import React from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import App from './App';
import './index.css';

window.__APP_API_BASE_URL__ = import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080';
window.__APP_SAMPLE_STUDENT_ID__ = import.meta.env.VITE_SAMPLE_STUDENT_ID || '1';

createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </React.StrictMode>
);
