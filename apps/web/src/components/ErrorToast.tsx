/**
 * Error toast/alert component
 */

import type { FunctionalComponent } from 'preact';
import './ErrorToast.css';

export interface ErrorToastProps {
  /** Error message to display */
  message: string;
  /** Callback to dismiss the error */
  onDismiss: () => void;
}

/**
 * Displays error messages as a dismissible toast notification
 */
export const ErrorToast: FunctionalComponent<ErrorToastProps> = ({ message, onDismiss }) => {
  return (
    <div class="error-toast" role="alert">
      <div class="error-content">
        <span class="error-icon">⚠️</span>
        <p class="error-message">{message}</p>
      </div>
      <button class="error-dismiss" onClick={onDismiss} aria-label="Dismiss error">
        ×
      </button>
    </div>
  );
};
