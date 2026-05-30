import { Typography, Table, TableBody, TableCell, TableHead, TableRow, Chip, Paper, CircularProgress, Alert, Card, CardContent, Stack, useMediaQuery, useTheme } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { getEmails } from '../api'

const classColor = (c: string): 'success' | 'default' | 'warning' =>
  c === 'Relevant' ? 'success' : c === 'Irrelevant' ? 'default' : 'warning'

const classLabel = (c: string) =>
  c === 'Relevant' ? 'Relevant' : c === 'Irrelevant' ? 'Irrelevant' : c

export default function EmailsPage() {
  const { data, isLoading, isError } = useQuery({ queryKey: ['emails'], queryFn: getEmails })
  const theme = useTheme()
  const isMobile = useMediaQuery(theme.breakpoints.down('md'))

  if (isLoading) return <CircularProgress />
  if (isError) return <Alert severity="error">Kunde inte hämta e-postlistan. Försök igen senare.</Alert>

  return (
    <>
      <Typography variant="h5" fontWeight={600} gutterBottom>Inkorg</Typography>
      {data?.length === 0 && <Alert severity="info" sx={{ mb: 2 }}>Inga e-postmeddelanden har bearbetats ännu.</Alert>}

      {isMobile ? (
        // Mobile: card list
        <Stack spacing={1.5}>
          {data?.map(email => (
            <Card key={email.id} variant="outlined">
              <CardContent sx={{ py: 1.5, '&:last-child': { pb: 1.5 } }}>
                <Typography variant="body2" fontWeight={600} noWrap>{email.subject}</Typography>
                <Typography variant="caption" color="text.secondary" noWrap display="block">
                  {email.sender.replace(/<.*>/, '').trim()}
                </Typography>
                <Stack direction="row" spacing={1} mt={1} alignItems="center" flexWrap="wrap">
                  <Chip label={classLabel(email.classification)} color={classColor(email.classification)} size="small" />
                  {email.confidence != null && (
                    <Chip
                      label={`${Math.round(email.confidence * 100)}%`}
                      color={email.confidence >= 0.8 ? 'success' : email.confidence >= 0.6 ? 'warning' : 'error'}
                      size="small"
                    />
                  )}
                  <Typography variant="caption" color="text.secondary" sx={{ ml: 'auto' }}>
                    {new Date(email.receivedAt).toLocaleDateString('sv-SE', { month: 'short', day: 'numeric' })}
                  </Typography>
                </Stack>
              </CardContent>
            </Card>
          ))}
        </Stack>
      ) : (
        // Desktop: table
        <Paper>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Ämne</TableCell>
                <TableCell>Avsändare</TableCell>
                <TableCell>Mottaget</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>AI-säkerhet</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data?.map(email => (
                <TableRow key={email.id}>
                  <TableCell>{email.subject}</TableCell>
                  <TableCell sx={{ maxWidth: 200, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {email.sender}
                  </TableCell>
                  <TableCell>{new Date(email.receivedAt).toLocaleString('sv-SE')}</TableCell>
                  <TableCell>
                    <Chip label={email.classification} color={classColor(email.classification)} size="small" />
                  </TableCell>
                  <TableCell>
                    {email.confidence != null ? (
                      <Chip
                        label={`${Math.round(email.confidence * 100)}%`}
                        color={email.confidence >= 0.8 ? 'success' : email.confidence >= 0.6 ? 'warning' : 'error'}
                        size="small"
                      />
                    ) : '–'}
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
