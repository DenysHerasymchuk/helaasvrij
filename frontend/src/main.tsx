import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, Navigate, RouterProvider, useLocation, useParams } from 'react-router'
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
import { ProjectLayout } from '@/features/project/project-layout'
import { SummaryPage } from '@/features/project/summary-page'
import { ListPage } from '@/features/project/list-page'
import { BacklogPage } from '@/features/backlog/backlog-page'
import { BoardPage } from '@/features/board/board-page'
import { SprintsPage } from '@/features/sprints/sprints-page'
import { SprintReportPage } from '@/features/sprints/sprint-report-page'
import { DocumentsPage } from '@/features/documents/documents-page'
import { InterviewPage } from '@/features/interview/interview-page'
import { GapsPage } from '@/features/gaps/gaps-page'
import { ConnectClaudePage } from '@/features/mcp/connect-claude-page'
import { CalendarPage } from '@/features/calendar/calendar-page'
import './index.css'

/** Redirect that fills the new path from the old route's params and keeps the query string and hash. */
function RedirectTo({ to }: { to: (params: Record<string, string | undefined>) => string }) {
  const params = useParams()
  const { search, hash } = useLocation()
  return <Navigate to={to(params) + search + hash} replace />
}

const router = createBrowserRouter([
  { path: '/', element: <Navigate to="/login" replace /> },
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  {
    element: <AppShell />,
    children: [
      { path: '/dashboard', element: <DashboardPage /> },
      { path: '/profile', element: <RedirectTo to={() => '/settings/profile'} /> },
      {
        path: '/settings',
        element: <SettingsLayout />,
        children: [
          { index: true, element: <Navigate to="preferences" replace /> },
          { path: 'preferences', element: <PreferencesTab /> },
          { path: 'profile', element: <ProfilePage /> },
          { path: 'notifications', element: <NotificationsTab /> },
          { path: 'account', element: <AccountTab /> },
          { path: 'data', element: <DataTab /> },
        ],
      },
      {
        element: <ProjectLayout />,
        children: [
          { path: '/summary', element: <SummaryPage /> },
          { path: '/list', element: <ListPage /> },
          { path: '/board', element: <BoardPage /> },
          { path: '/backlog', element: <BacklogPage /> },
          { path: '/backlog/find', element: <JobsPage /> },
          { path: '/backlog/find/:jobId', element: <JobDetailPage /> },
          { path: '/sprints', element: <SprintsPage /> },
          { path: '/sprints/:id', element: <SprintReportPage /> },
        ],
      },
      { path: '/positions/:id', element: <PostingDetailPage /> },
      // Old addresses keep working.
      { path: '/postings', element: <Navigate to="/backlog/find" replace /> },
      { path: '/postings/saved', element: <Navigate to="/backlog" replace /> },
      { path: '/postings/job/:jobId', element: <RedirectTo to={(p) => `/backlog/find/${encodeURIComponent(p.jobId ?? '')}`} /> },
      { path: '/postings/:id', element: <RedirectTo to={(p) => `/positions/${p.id}`} /> },
      { path: '/jobs', element: <Navigate to="/backlog/find" replace /> },
      { path: '/documents', element: <DocumentsPage /> },
      { path: '/applications', element: <Navigate to="/board" replace /> },
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
