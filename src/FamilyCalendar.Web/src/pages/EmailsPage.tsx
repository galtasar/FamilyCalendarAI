import { Typography, Table, TableBody, TableCell, TableHead, TableRow, Chip, Paper, CircularProgress } from '@mui/material'
import { useQuery } from '@tanstack/react-query'
import { getEmails } from '../api'

const classColor = (c: string): 'success' | 'default' | 'warning' =>
  c === 'Relevant' ? 'success' : c === 'Irrelevant' ? 'default' : 'warning'

export default function EmailsPage() {
  const { data, isLoading } = useQuery({ queryKey: ['emails'], queryFn: getEmails })

  if (isLoading) return <CircularProgress />

  return (
    <>
      <Typography variant="h4" gutterBottom>Inkorg</Typography>
      <Paper>
        <Table>
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
                <TableCell>{email.sender}</TableCell>
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
    </>
  )
}
