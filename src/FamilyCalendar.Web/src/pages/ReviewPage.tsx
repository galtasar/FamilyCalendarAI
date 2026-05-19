import { Typography, Card, CardContent, CardActions, Button, Chip, Stack, CircularProgress, Alert } from '@mui/material'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getPendingReview, approveEvent, rejectEvent } from '../api'
import { useNavigate } from 'react-router-dom'

const familyMemberColor: Record<string, 'primary' | 'secondary' | 'success' | 'warning'> = {
  Vera: 'primary', Tage: 'secondary', Sixten: 'success', Folke: 'warning'
}

export default function ReviewPage() {
  const { data, isLoading } = useQuery({ queryKey: ['pending'], queryFn: getPendingReview })
  const qc = useQueryClient()
  const navigate = useNavigate()

  const approveMutation = useMutation({
    mutationFn: approveEvent,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['pending'] })
  })
  const rejectMutation = useMutation({
    mutationFn: rejectEvent,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['pending'] })
  })

  if (isLoading) return <CircularProgress />

  return (
    <>
      <Typography variant="h4" gutterBottom>Granskning</Typography>
      {data?.length === 0 && <Alert severity="success">Inga händelser väntar på granskning.</Alert>}
      <Stack spacing={2}>
        {data?.map(evt => (
          <Card key={evt.id} sx={{ borderLeft: '4px solid orange' }}>
            <CardContent>
              <Stack direction="row" spacing={1} mb={1}>
                <Chip label={evt.familyMemberName} color={familyMemberColor[evt.familyMemberName] ?? 'default'} size="small" />
                <Chip label={evt.calendarProvider} size="small" variant="outlined" />
              </Stack>
              <Typography variant="h6">{evt.title}</Typography>
              <Typography color="text.secondary">
                {new Date(evt.startTime).toLocaleString('sv-SE', { dateStyle: 'full', timeStyle: 'short' })}
                {evt.location ? ` • ${evt.location}` : ''}
              </Typography>
              {evt.description && <Typography variant="body2" mt={1}>{evt.description}</Typography>}
            </CardContent>
            <CardActions>
              <Button color="success" variant="contained" size="small" onClick={() => approveMutation.mutate(evt.id)}>✅ Godkänn</Button>
              <Button color="error" variant="outlined" size="small" onClick={() => rejectMutation.mutate(evt.id)}>❌ Avvisa</Button>
              <Button size="small" onClick={() => navigate(`/review/${evt.id}`)}>🔍 Detaljer</Button>
            </CardActions>
          </Card>
        ))}
      </Stack>
    </>
  )
}
