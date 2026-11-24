import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router'
import { AppShell } from '@/core/app-shell'
import { AuthProvider } from '@/core/auth'
import { LoginPage } from '@/features/auth/login-page'
import { RegisterPage } from '@/features/auth/register-page'
import { DashboardPage } from '@/features/dashboard/dashboard-page'
import { SettingsPage } from '@/features/settings/settings-page'
import { ComingSoonPage } from '@/features/placeholder/coming-soon-page'
import './index.css'

const placeholders = [
  ['postings', 'Postings'],
  ['gaps', 'Gap plan'],
  ['documents', 'Documents'],
  ['interview', 'Interview prep'],
  ['applications', 'Applications'],
  ['profile', 'Profile'],
] as const

const router = createBrowserRouter([
  { path: '/', element: <Navigate to="/login" replace /> },
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  {
    element: <AppShell />,
    children: [
      { path: '/dashboard', element: <DashboardPage /> },
      { path: '/settings', element: <SettingsPage /> },
      ...placeholders.map(([path, title]) => ({ path: `/${path}`, element: <ComingSoonPage title={title} /> })),
    ],
  },
])

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>
  </StrictMode>,
)
