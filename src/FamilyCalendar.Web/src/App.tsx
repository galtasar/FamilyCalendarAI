import { useState } from 'react'
import { Routes, Route, Link, useLocation } from 'react-router-dom'
import {
  AppBar, Toolbar, Typography, Container, BottomNavigation, BottomNavigationAction,
  Box, IconButton, Drawer, List, ListItemButton, ListItemText, useMediaQuery, useTheme
} from '@mui/material'
import MenuIcon from '@mui/icons-material/Menu'
import DashboardPage from './pages/DashboardPage'
import EmailsPage from './pages/EmailsPage'
import ReviewPage from './pages/ReviewPage'
import ReviewDetailPage from './pages/ReviewDetailPage'
import EventsPage from './pages/EventsPage'
import ReviewActionPage from './pages/ReviewActionPage'
import FamilyMembersPage from './pages/FamilyMembersPage'

const navItems = [
  { label: 'Översikt', path: '/', icon: '🏠' },
  { label: 'Granskning', path: '/review', icon: '📋' },
  { label: 'Händelser', path: '/events', icon: '📅' },
  { label: 'Inkorg', path: '/emails', icon: '📧' },
  { label: 'Familj', path: '/familymembers', icon: '👨‍👩‍👧‍👦' },
]

export default function App() {
  const theme = useTheme()
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'))
  const location = useLocation()
  const [drawerOpen, setDrawerOpen] = useState(false)

  const currentIndex = navItems.findIndex(n =>
    n.path === '/' ? location.pathname === '/' : location.pathname.startsWith(n.path)
  )

  return (
    <>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>📅 Familjekalender AI</Typography>
          {isMobile ? (
            <>
              <IconButton color="inherit" onClick={() => setDrawerOpen(true)}>
                <MenuIcon />
              </IconButton>
              <Drawer anchor="right" open={drawerOpen} onClose={() => setDrawerOpen(false)}>
                <Box sx={{ width: 220 }} onClick={() => setDrawerOpen(false)}>
                  <List>
                    {navItems.map(item => (
                      <ListItemButton key={item.path} component={Link} to={item.path}
                        selected={item.path === '/' ? location.pathname === '/' : location.pathname.startsWith(item.path)}>
                        <ListItemText primary={`${item.icon} ${item.label}`} />
                      </ListItemButton>
                    ))}
                  </List>
                </Box>
              </Drawer>
            </>
          ) : (
            navItems.map(item => (
              <Box key={item.path} component={Link} to={item.path}
                sx={{ color: 'inherit', textDecoration: 'none', mx: 1, opacity: currentIndex === navItems.indexOf(item) ? 1 : 0.75, '&:hover': { opacity: 1 } }}>
                <Typography variant="button">{item.label}</Typography>
              </Box>
            ))
          )}
        </Toolbar>
      </AppBar>

      <Container maxWidth="lg" sx={{ mt: 2, mb: isMobile ? 8 : 3, px: isMobile ? 1.5 : 3 }}>
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

      {isMobile && (
        <BottomNavigation value={currentIndex} showLabels
          sx={{ position: 'fixed', bottom: 0, left: 0, right: 0, zIndex: 1300, borderTop: '1px solid #e0e0e0' }}>
          {navItems.map(item => (
            <BottomNavigationAction key={item.path} label={item.label}
              icon={<Typography fontSize={20}>{item.icon}</Typography>}
              component={Link} to={item.path} />
          ))}
        </BottomNavigation>
      )}
    </>
  )
}
