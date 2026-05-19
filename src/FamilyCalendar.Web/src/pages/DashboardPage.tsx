import { Grid, Card, CardContent, Typography, CircularProgress } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { getEmails, getPendingReview, getEvents } from '../api'

export default function DashboardPage() {
  const { data: emails, isLoading: loadingEmails } = useQuery({ queryKey: ['emails'], queryFn: getEmails })
  const { data: pending, isLoading: loadingPending } = useQuery({ queryKey: ['pending'], queryFn: getPendingReview })
  const { data: events, isLoading: loadingEvents } = useQuery({ queryKey: ['events'], queryFn: () => getEvents() })

  return (
    <>
      <Typography variant="h4" gutterBottom>Översikt</Typography>
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
