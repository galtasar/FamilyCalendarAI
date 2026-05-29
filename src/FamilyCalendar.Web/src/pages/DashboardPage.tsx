import { useState } from 'react'
import { Grid, Card, CardContent, Typography, CircularProgress, Button, Alert, Stack } from '@mui/material'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { getEmails, getPendingReview, getEvents, syncEmails } from '../api'

export default function DashboardPage() {
  const qc = useQueryClient()
  const { data: emails, isLoading: loadingEmails } = useQuery({ queryKey: ['emails'], queryFn: getEmails })
  const { data: pending, isLoading: loadingPending } = useQuery({ queryKey: ['pending'], queryFn: getPendingReview })
  const { data: events, isLoading: loadingEvents } = useQuery({ queryKey: ['events'], queryFn: () => getEvents() })

  const [syncing, setSyncing] = useState(false)
  const [syncError, setSyncError] = useState(false)
  const [syncDone, setSyncDone] = useState(false)

  const handleSync = async () => {
    setSyncing(true)
    setSyncError(false)
    setSyncDone(false)
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

  return (
    <>
      <Stack direction="row" alignItems="center" justifyContent="space-between" mb={3}>
        <Typography variant="h4">Översikt</Typography>
        <Stack direction="row" spacing={2} alignItems="center">
          {syncDone && <Alert severity="success" sx={{ py: 0 }}>Synkronisering klar!</Alert>}
          {syncError && <Alert severity="error" sx={{ py: 0 }}>Synkronisering misslyckades.</Alert>}
          <Button
            variant="contained"
            onClick={handleSync}
            disabled={syncing}
            startIcon={syncing ? <CircularProgress size={16} color="inherit" /> : null}
          >
            {syncing ? 'Hämtar mail...' : '🔄 Synka mail'}
          </Button>
        </Stack>
      </Stack>
      <Grid container spacing={3}>
        <Grid item xs={4}>
          <Card>
            <CardContent>
              <Typography color="text.secondary">Senaste mail</Typography>
              <Typography variant="h3">{loadingEmails ? <CircularProgress size={24} /> : emails?.length ?? 0}</Typography>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={4}>
          <Card sx={{ borderLeft: '4px solid orange' }}>
            <CardContent>
              <Typography color="text.secondary">Väntar på granskning</Typography>
              <Typography variant="h3">{loadingPending ? <CircularProgress size={24} /> : pending?.length ?? 0}</Typography>
            </CardContent>
          </Card>
        </Grid>
        <Grid item xs={4}>
          <Card sx={{ borderLeft: '4px solid green' }}>
            <CardContent>
              <Typography color="text.secondary">Kommande händelser</Typography>
              <Typography variant="h3">{loadingEvents ? <CircularProgress size={24} /> : events?.length ?? 0}</Typography>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </>
  )
}
