import { TIMING } from '@/lib/constants';

setTimeout(() => {
  setStatusState({ status: 'idle' });
}, TIMING.STATUS_AUTO_DISMISS_MS);
function setStatusState(_arg0: { status: string; }) {
    throw new Error('Function not implemented.');
}

