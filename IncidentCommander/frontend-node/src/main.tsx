import '@ikonai/sdk-react-ui/fonts/ikon-fonts.css';
import '@ikonai/sdk-react-ui/theme/ikon-tokens.css';
import '@ikonai/sdk-react-ui/theme/ikon-surface.css';
import '@ikonai/sdk-react-ui/theme/ikon-app.css';
import '@ikonai/sdk-react-ui/theme/ikon-auth.css';
import './main.css';

import { createRoot } from 'react-dom/client';
import App from './app';

const root = document.getElementById('root');
if (!root) throw new Error('Missing root element');
createRoot(root).render(<App />);
