import { Typography, Table, TableBody, TableCell, TableHead, TableRow, Chip, Paper, CircularProgress } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { getEvents } from '../api'

const statusColor = (s: string): 'success' | 'error' | 'warning' | 'default' =>
  s === 'Created' ? 'success' : s === 'Rejected' ? 'error' : s === 'Pending' ? 'warning' : 'default'

export default function EventsPage() {
  const { data, isLoading } = useQuery({ queryKey: ['events'], queryFn: () => getEvents() })

  if (isLoading) return <CircularProgress />

  return (
    <>
      <Typography variant="h4" gutterBottom>Händelser</Typography>
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
                <TableCell>{new Date(evt.startTime).toLocaleString('sv-SE')}</TableCell>
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
