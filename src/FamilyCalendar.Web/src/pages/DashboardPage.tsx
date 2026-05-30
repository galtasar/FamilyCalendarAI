import { useState } from 'react'
import { Grid, Card, CardContent, Typography, CircularProgress, Button, Alert, Stack, Chip, Box, Divider } from '@mui/material'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { getEmails, getPendingReview, getEvents, syncEmails } from '../api'

export default function DashboardPage() {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const { data: emails, isLoading: loadingEmails } = useQuery({ queryKey: ['emails'], queryFn: getEmails })
  const { data: pending, isLoading: loadingPending } = useQuery({ queryKey: ['pending'], queryFn: getPendingReview })

  const now = new Date()
  const in14days = new Date(now)
  in14days.setDate(now.getDate() + 14)
  const { data: events, isLoading: loadingEvents } = useQuery({
    queryKey: ['events', 'upcoming'],
    queryFn: () => getEvents({ from: now.toISOString(), to: in14days.toISOString() })
  })

  const [syncing, setSyncing] = useState(false)
  const [syncError, setSyncError] = useState(false)
  const [syncDone, setSyncDone] = useState(false)

  const handleSync = async () => {
    setSyncing(true); setSyncError(false); setSyncDone(false)
    try {
      await syncEmails()
      await qc.invalidateQueries()
      setSyncDone(true)
    } catch {
      setSyncError(true)
    } finally {
      setSyncing(false)
    }
  }

  const upcomingEvents = (events ?? [])
    .filter(e => e.status !== 'Rejected')
    .sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime())

  return (
    <>
      <Stack direction="row" alignItems="center" justifyContent="space-between" mb={2} flexWrap="wrap" gap={1}>
        <Typography variant="h5" fontWeight={600}>Översikt</Typography>
        <Button
          variant="contained"
          onClick={handleSync}
          disabled={syncing}
          size="small"
          startIcon={syncing ? <CircularProgress size={14} color="inherit" /> : null}
        >
          {syncing ? 'Hämtar...' : '🔄 Synka mail'}
        </Button>
      </Stack>

      {syncDone && <Alert severity="success" sx={{ mb: 2 }}>Synkronisering klar!</Alert>}
      {syncError && <Alert severity="error" sx={{ mb: 2 }}>Synkronisering misslyckades.</Alert>}

      {/* Stat cards */}
      <Grid container spacing={2} mb={3}>
        <Grid item xs={4}>
          <Card sx={{ textAlign: 'center' }}>
            <CardContent sx={{ py: 2, '&:last-child': { pb: 2 } }}>
              <Typography variant="caption" color="text.secondary" display="block">Mail</Typography>
              <Typography variant="h4" fontWeight={700}>
                {loadingEmails ? <CircularProgress size={20} /> : emails?.length ?? 0}
              </Typography>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={4}>
          <Card sx={{ textAlign: 'center', borderTop: '3px solid orange', cursor: 'pointer' }} onClick={() => navigate('/review')}>
            <CardContent sx={{ py: 2, '&:last-child': { pb: 2 } }}>
              <Typography variant="caption" color="text.secondary" display="block">Granskning</Typography>
              <Typography variant="h4" fontWeight={700} color={pending?.length ? 'warning.main' : 'text.primary'}>
                {loadingPending ? <CircularProgress size={20} /> : pending?.length ?? 0}
              </Typography>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={4}>
          <Card sx={{ textAlign: 'center', borderTop: '3px solid green' }}>
            <CardContent sx={{ py: 2, '&:last-child': { pb: 2 } }}>
              <Typography variant="caption" color="text.secondary" display="block">Kommande</Typography>
              <Typography variant="h4" fontWeight={700} color="success.main">
                {loadingEvents ? <CircularProgress size={20} /> : upcomingEvents.length}
              </Typography>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Pending review shortcut */}
      {(pending?.length ?? 0) > 0 && (
        <Alert severity="warning" sx={{ mb: 2 }} action={
          <Button color="inherit" size="small" onClick={() => navigate('/review')}>Granska</Button>
        }>
          {pending!.length} händelse{pending!.length > 1 ? 'r' : ''} väntar på granskning
        </Alert>
      )}

      {/* Upcoming events */}
      <Typography variant="h6" fontWeight={600} mb={1}>Kommande 14 dagar</Typography>
      <Card>
        {loadingEvents ? (
          <CardContent><CircularProgress size={24} /></CardContent>
        ) : upcomingEvents.length === 0 ? (
          <CardContent>
            <Typography color="text.secondary" variant="body2">Inga kommande händelser.</Typography>
          </CardContent>
        ) : (
          upcomingEvents.map((evt, i) => {
            const start = new Date(evt.startTime)
            const isToday = start.toDateString() === now.toDateString()
            const isTomorrow = start.toDateString() === new Date(now.getTime() + 86400000).toDateString()
            const dayLabel = isToday ? 'Idag' : isTomorrow ? 'Imorgon'
              : start.toLocaleDateString('sv-SE', { weekday: 'short', month: 'short', day: 'numeric' })
            const timeLabel = evt.endTime
              ? start.toLocaleTimeString('sv-SE', { hour: '2-digit', minute: '2-digit' })
              : 'Heldag'

            return (
              <Box key={evt.id}>
                {i > 0 && <Divider />}
                <Box sx={{ px: 2, py: 1.5, display: 'flex', alignItems: 'center', gap: 1.5 }}>
                  <Box sx={{ minWidth: 60, textAlign: 'center' }}>
                    <Typography variant="caption" color={isToday ? 'primary.main' : 'text.secondary'} fontWeight={isToday ? 700 : 400} display="block">
                      {dayLabel}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">{timeLabel}</Typography>
                  </Box>
                  <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                    <Typography variant="body2" fontWeight={600} noWrap>{evt.title}</Typography>
                    <Stack direction="row" spacing={0.5} mt={0.25} flexWrap="wrap">
                      {evt.familyMemberName.split(',').map(m => (
                        <Chip key={m.trim()} label={m.trim()} size="small" sx={{ height: 18, fontSize: 10 }} />
                      ))}
                      {evt.location && (
                        <Typography variant="caption" color="text.secondary" noWrap>📍 {evt.location}</Typography>
                      )}
                    </Stack>
                  </Box>
                </Box>
              </Box>
            )
          })
        )}
      </Card>
    </>
  )
}
