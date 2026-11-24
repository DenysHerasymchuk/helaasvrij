import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router'
import { AppShell } from '@/core/app-shell'
import { AuthProvider } from '@/core/auth'
import { LoginPage } from '@/features/auth/login-page'
import { RegisterPage } from '@/features/auth/register-page'
import { DashboardPage } from '@/features/dashboard/dashboard-page'
import { ProfilePage } from '@/features/profile/profile-page'
import { SettingsPage } from '@/features/settings/settings-page'
import { PostingDetailPage } from '@/features/postings/posting-detail-page'
import { JobsPage } from '@/features/jobs/jobs-page'
import { PostingsPage } from '@/features/postings/postings-page'
import { DocumentsPage } from '@/features/documents/documents-page'
import { ApplicationsPage } from '@/features/applications/applications-page'
import { ComingSoonPage } from '@/features/placeholder/coming-soon-page'
import './index.css'

const placeholders = [
  ['gaps', 'Gap plan'],
  ['interview', 'Interview prep'],
] as const

const router = createBrowserRouter([
  { path: '/', element: <Navigate to="/login" replace /> },
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  {
    element: <AppShell />,
    children: [
      { path: '/dashboard', element: <DashboardPage /> },
      { path: '/profile', element: <ProfilePage /> },
      { path: '/settings', element: <SettingsPage /> },
      { path: '/postings', element: <PostingsPage /> },
      { path: '/postings/:id', element: <PostingDetailPage /> },
      { path: '/jobs', element: <JobsPage /> },
      { path: '/documents', element: <DocumentsPage /> },
      { path: '/applications', element: <ApplicationsPage /> },
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
