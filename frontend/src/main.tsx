import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, Navigate, RouterProvider } from 'react-router'
import { AppShell } from '@/core/app-shell'
import { AuthProvider } from '@/core/auth'
import { LoginPage } from '@/features/auth/login-page'
import { RegisterPage } from '@/features/auth/register-page'
import { DashboardPage } from '@/features/dashboard/dashboard-page'
import { ProfilePage } from '@/features/profile/profile-page'
import { SettingsLayout } from '@/features/settings/settings-layout'
import { PreferencesTab } from '@/features/settings/preferences-tab'
import { NotificationsTab } from '@/features/settings/notifications-tab'
import { AccountTab } from '@/features/settings/account-tab'
import { DataTab } from '@/features/settings/data-tab'
import { PostingDetailPage } from '@/features/postings/posting-detail-page'
import { JobsPage } from '@/features/jobs/jobs-page'
import { JobDetailPage } from '@/features/jobs/job-detail-page'
import { PostingsPage } from '@/features/postings/postings-page'
import { DocumentsPage } from '@/features/documents/documents-page'
import { ApplicationsPage } from '@/features/applications/applications-page'
import { InterviewPage } from '@/features/interview/interview-page'
import { GapsPage } from '@/features/gaps/gaps-page'
import { ConnectClaudePage } from '@/features/mcp/connect-claude-page'
import { CalendarPage } from '@/features/calendar/calendar-page'
import './index.css'

const router = createBrowserRouter([
  { path: '/', element: <Navigate to="/login" replace /> },
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  {
    element: <AppShell />,
    children: [
      { path: '/dashboard', element: <DashboardPage /> },
      { path: '/profile', element: <ProfilePage /> },
      {
        path: '/settings',
        element: <SettingsLayout />,
        children: [
          { index: true, element: <Navigate to="preferences" replace /> },
          { path: 'preferences', element: <PreferencesTab /> },
          { path: 'notifications', element: <NotificationsTab /> },
          { path: 'account', element: <AccountTab /> },
          { path: 'data', element: <DataTab /> },
        ],
      },
      { path: '/postings', element: <JobsPage /> },
      { path: '/postings/saved', element: <PostingsPage /> },
      { path: '/postings/job/:jobId', element: <JobDetailPage /> },
      { path: '/postings/:id', element: <PostingDetailPage /> },
      { path: '/jobs', element: <Navigate to="/postings" replace /> },
      { path: '/documents', element: <DocumentsPage /> },
      { path: '/applications', element: <ApplicationsPage /> },
      { path: '/interview', element: <InterviewPage /> },
      { path: '/gaps', element: <GapsPage /> },
      { path: '/connect-claude', element: <ConnectClaudePage /> },
      { path: '/calendar', element: <CalendarPage /> },
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
