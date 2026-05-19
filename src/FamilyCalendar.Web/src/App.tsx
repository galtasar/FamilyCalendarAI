import { Routes, Route, Link } from 'react-router-dom'
import { AppBar, Toolbar, Typography, Button, Container } from '@mui/material'
import DashboardPage from './pages/DashboardPage'
import EmailsPage from './pages/EmailsPage'
import ReviewPage from './pages/ReviewPage'
import ReviewDetailPage from './pages/ReviewDetailPage'
import EventsPage from './pages/EventsPage'
import ReviewActionPage from './pages/ReviewActionPage'
import FamilyMembersPage from './pages/FamilyMembersPage'

export default function App() {
  return (
    <>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>📅 Familjekalender AI</Typography>
          <Button color="inherit" component={Link} to="/">Översikt</Button>
          <Button color="inherit" component={Link} to="/emails">Inkorg</Button>
          <Button color="inherit" component={Link} to="/review">Granskning</Button>
          <Button color="inherit" component={Link} to="/events">Händelser</Button>
          <Button color="inherit" component={Link} to="/familymembers">Familj</Button>
        </Toolbar>
      </AppBar>
      <Container maxWidth="lg" sx={{ mt: 3 }}>
        <Routes>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/emails" element={<EmailsPage />} />
          <Route path="/review" element={<ReviewPage />} />
          <Route path="/review/:id" element={<ReviewDetailPage />} />
          <Route path="/review/:id/approve" element={<ReviewActionPage action="approve" />} />
          <Route path="/review/:id/reject" element={<ReviewActionPage action="reject" />} />
          <Route path="/events" element={<EventsPage />} />
          <Route path="/familymembers" element={<FamilyMembersPage />} />
          <Route path="/children" element={<FamilyMembersPage />} />
        </Routes>
      </Container>
    </>
  )
}
