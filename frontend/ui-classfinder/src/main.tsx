import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';

import App from './App';
import { setRuntimeConfig } from './config/runtime';
import './index.css';

setRuntimeConfig({
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? '',
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
);
