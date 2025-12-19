import { useReducer, useCallback } from 'react';
import { ThunderstoreClient } from '@/lib/thunderstore/client';
import { ModPackage, normalizePackage } from '@/lib/thunderstore/normalize';
import { isCosmeticMod, searchMods } from '@/lib/thunderstore/mod-filters';

// State interface
interface ModDataState {
  allMods: ModPackage[];
  filteredMods: ModPackage[];
  selectedMod: ModPackage | null;
  loading: boolean;
  error: string | null;
}

// Action types
type ModDataAction =
  | { type: 'LOAD_START' }
  | { type: 'LOAD_SUCCESS'; payload: ModPackage[] }
  | { type: 'LOAD_ERROR'; payload: string }
  | { type: 'SEARCH'; payload: string }
  | { type: 'SELECT_MOD'; payload: ModPackage | null };

// Reducer
function modDataReducer(state: ModDataState, action: ModDataAction): ModDataState {
  switch (action.type) {
    case 'LOAD_START':
      return {
        ...state,
        loading: true,
        error: null,
        selectedMod: null,
      };
    
    case 'LOAD_SUCCESS':
      return {
        ...state,
        loading: false,
        allMods: action.payload,
        filteredMods: action.payload,
      };
    
    case 'LOAD_ERROR':
      return {
        ...state,
        loading: false,
        error: action.payload,
      };
    
    case 'SEARCH':
      return {
        ...state,
        filteredMods: searchMods(state.allMods, action.payload),
      };
    
    case 'SELECT_MOD':
      return {
        ...state,
        selectedMod: action.payload,
      };
    
    default:
      return state;
  }
}

// Initial state
const initialState: ModDataState = {
  allMods: [],
  filteredMods: [],
  selectedMod: null,
  loading: true,
  error: null,
};

// Hook interface
export interface UseModDataReturn {
  mods: ModPackage[];
  selectedMod: ModPackage | null;
  loading: boolean;
  error: string | null;
  search: (query: string) => void;
  selectMod: (mod: ModPackage | null) => void;
  retry: () => Promise<void>;
  loadMods: () => Promise<void>;
}

/**
 * Custom hook for managing mod data, filtering, and selection
 */
export function useModData(client: ThunderstoreClient): UseModDataReturn {
  const [state, dispatch] = useReducer(modDataReducer, initialState);

  const loadMods = useCallback(async () => {
    dispatch({ type: 'LOAD_START' });
    
    try {
      // Fetch packages from Thunderstore API
      const packages = await client.listPackagesV1('repo');
      
      // Filter cosmetic mods and normalize
      const cosmeticMods = packages
        .filter(isCosmeticMod)
        .map(normalizePackage);
      
      dispatch({ type: 'LOAD_SUCCESS', payload: cosmeticMods });
    } catch (error) {
      const msg = error instanceof Error ? error.message : 'Unknown error';
      console.error('Failed to load mods:', error);
      dispatch({ type: 'LOAD_ERROR', payload: `Failed to load mods: ${msg}` });
    }
  }, [client]);

  const search = useCallback((query: string) => {
    dispatch({ type: 'SEARCH', payload: query });
  }, []);

  const selectMod = useCallback((mod: ModPackage | null) => {
    dispatch({ type: 'SELECT_MOD', payload: mod });
  }, []);

  const retry = useCallback(async () => {
    await loadMods();
  }, [loadMods]);

  return {
    mods: state.filteredMods,
    selectedMod: state.selectedMod,
    loading: state.loading,
    error: state.error,
    search,
    selectMod,
    retry,
    loadMods,
  };
}
