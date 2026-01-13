import { render } from 'preact';
import { App } from './App';

/**
 * Entry point for the REPO Cosmetic Viewer web app
 * Mounts the Preact application to the DOM
 */
const root = document.getElementById('app');

if (!root) {
  throw new Error('Root element #app not found in DOM');
}

render(<App />, root);
