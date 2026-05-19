import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Typography, CircularProgress, Alert, Box } from '@mui/material'
import { approveEvent, rejectEvent } from '../api'

interface Props { action: 'approve' | 'reject' }

export default function ReviewActionPage({ action }: Props) {
  const { id } = useParams<{ id: string }>()
  const [status, setStatus] = useState<'loading' | 'success' | 'error'>('loading')

  useEffect(() => {
    if (!id) return
    const fn = action === 'approve' ? approveEvent : rejectEvent
    fn(id).then(() => setStatus('success')).catch(() => setStatus('error'))
  }, [id, action])

  return (
    <Box mt={8} textAlign="center">
      {status === 'loading' && <CircularProgress />}
      {status === 'success' && (
        <Alert severity="success" sx={{ maxWidth: 400, mx: 'auto' }}>
          <Typography variant="h6">{action === 'approve' ? '✅ Händelsen godkändes!' : '❌ Händelsen avvisades!'}</Typography>
          <Typography>Du kan stänga den här fliken.</Typography>
        </Alert>
      )}
      {status === 'error' && (
        <Alert severity="error" sx={{ maxWidth: 400, mx: 'auto' }}>
          <Typography>Något gick fel. Händelsen kanske redan har behandlats.</Typography>
        </Alert>
      )}
    </Box>
  )
}
