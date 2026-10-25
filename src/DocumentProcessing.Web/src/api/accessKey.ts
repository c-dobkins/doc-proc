// The access key unlocks write actions on a public deployment. It is kept in localStorage so it
// survives reloads; storage can be unavailable (private mode, blocked site data), so every access
// is guarded and the app keeps working read-only.
const STORAGE_KEY = 'docproc.accessKey';

export const ACCESS_KEY_HEADER = 'X-Api-Key';

export function getAccessKey(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

export function setAccessKey(key: string | null): void {
  try {
    if (key) localStorage.setItem(STORAGE_KEY, key);
    else localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Ignore: the key just won't persist
  }
}
