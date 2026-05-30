import { Typography, Table, TableBody, TableCell, TableHead, TableRow, Chip, Paper, CircularProgress, Alert, Card, CardContent, Stack, useMediaQuery, useTheme } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { getEvents } from '../api'

const statusColor = (s: string): 'success' | 'error' | 'warning' | 'default' =>
  s === 'Created' ? 'success' : s === 'Rejected' ? 'error' : s === 'Pending' ? 'warning' : 'default'

const statusLabel = (s: string) =>
  s === 'Created' ? 'Skapad' : s === 'Rejected' ? 'Avvisad' : s === 'Pending' ? 'Väntar' : s

export default function EventsPage() {
  const { data, isLoading, isError } = useQuery({ queryKey: ['events'], queryFn: () => getEvents() })
  const theme = useTheme()
  const isMobile = useMediaQuery(theme.breakpoints.down('sm'))

  if (isLoading) return <CircularProgress />
  if (isError) return <Alert severity="error">Kunde inte hämta händelselistan. Försök igen senare.</Alert>

  return (
    <>
      <Typography variant="h5" fontWeight={600} gutterBottom>Händelser</Typography>
      {data?.length === 0 && <Alert severity="info" sx={{ mb: 2 }}>Inga händelser har skapats ännu.</Alert>}

      {isMobile ? (
        <Stack spacing={1.5}>
          {data?.map(evt => (
            <Card key={evt.id} variant="outlined">
              <CardContent sx={{ py: 1.5, '&:last-child': { pb: 1.5 } }}>
                <Stack direction="row" justifyContent="space-between" alignItems="flex-start">
                  <Typography variant="body2" fontWeight={600} sx={{ flex: 1, mr: 1 }}>{evt.title}</Typography>
                  <Chip label={statusLabel(evt.status)} color={statusColor(evt.status)} size="small" />
                </Stack>
                <Typography variant="caption" color="text.secondary" display="block" mt={0.5}>
                  {evt.familyMemberName || '–'}
                </Typography>
                <Stack direction="row" justifyContent="space-between" mt={0.5}>
                  <Typography variant="caption" color="text.secondary">
                    {evt.endTime
                      ? new Date(evt.startTime).toLocaleDateString('sv-SE', { month: 'short', day: 'numeric' }) +
                        ' ' + new Date(evt.startTime).toLocaleTimeString('sv-SE', { hour: '2-digit', minute: '2-digit' })
                      : new Date(evt.startTime).toLocaleDateString('sv-SE', { month: 'short', day: 'numeric' }) + ' (heldag)'}
                  </Typography>
                  {evt.location && (
                    <Typography variant="caption" color="text.secondary" noWrap sx={{ maxWidth: 140 }}>
                      📍 {evt.location}
                    </Typography>
                  )}
                </Stack>
              </CardContent>
            </Card>
          ))}
        </Stack>
      ) : (
        <Paper>
          <Table size="small">
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
      )}
    </>
  )
}
