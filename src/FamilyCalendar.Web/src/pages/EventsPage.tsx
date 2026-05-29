import { Typography, Table, TableBody, TableCell, TableHead, TableRow, Chip, Paper, CircularProgress, Alert } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { getEvents } from '../api'

const statusColor = (s: string): 'success' | 'error' | 'warning' | 'default' =>
  s === 'Created' ? 'success' : s === 'Rejected' ? 'error' : s === 'Pending' ? 'warning' : 'default'

export default function EventsPage() {
  const { data, isLoading, isError } = useQuery({ queryKey: ['events'], queryFn: () => getEvents() })

  if (isLoading) return <CircularProgress />
  if (isError) return <Alert severity="error">Kunde inte hämta händelselistan. Försök igen senare.</Alert>

  return (
    <>
      <Typography variant="h4" gutterBottom>Händelser</Typography>
      {data?.length === 0 && <Alert severity="info" sx={{ mb: 2 }}>Inga händelser har skapats ännu.</Alert>}
      <Paper>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Titel</TableCell>
              <TableCell>Familjemedlem</TableCell>
              <TableCell>Starttid</TableCell>
              <TableCell>Plats</TableCell>
              <TableCell>Status</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data?.map(evt => (
              <TableRow key={evt.id}>
                <TableCell>{evt.title}</TableCell>
                <TableCell>{evt.familyMemberName || '–'}</TableCell>
                <TableCell>
                  {evt.endTime
                    ? new Date(evt.startTime).toLocaleString('sv-SE')
                    : `${new Date(evt.startTime).toLocaleDateString('sv-SE')} (heldag)`}
                </TableCell>
                <TableCell>{evt.location ?? '–'}</TableCell>
                <TableCell>
                  <Chip label={evt.status} color={statusColor(evt.status)} size="small" />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>
    </>
  )
}
