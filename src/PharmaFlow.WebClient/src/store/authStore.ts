import { create } from 'zustand';

interface User {
  userId: string;
  displayName: string;
  role: string;
  permissions: string[];
}

interface AuthState {
  token: string | null;
  user: User | null;
  isLoggedIn: boolean;
  canWrite: boolean;
  login: (token: string, user: User) => void;
  logout: () => void;
}

const getUser = (): User | null => {
  try { return JSON.parse(localStorage.getItem('user') || 'null'); } catch { return null; }
};

export const useAuthStore = create<AuthState>((set) => {
  const u = getUser();
  const canWrite = u ? (u.permissions.includes('*') || u.permissions.some(p => p.endsWith(':write'))) : false;
  return {
    token: localStorage.getItem('token'),
    user: u,
    isLoggedIn: !!localStorage.getItem('token'),
    canWrite,

    login: (token, user) => {
      localStorage.setItem('token', token);
      localStorage.setItem('user', JSON.stringify(user));
      const cw = user.permissions.includes('*') || user.permissions.some(p => p.endsWith(':write'));
      set({ token, user, isLoggedIn: true, canWrite: cw });
    },

    logout: () => {
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      set({ token: null, user: null, isLoggedIn: false, canWrite: false });
    },
  };
});
