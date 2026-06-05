import { useState } from 'react'
import { Box, Button, Card, CardContent, TextField, Typography, Alert, CircularProgress } from '@mui/material'
import { login } from '../api'

interface Props {
  onLogin: () => void
}

export default function LoginPage({ onLogin }: Props) {
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setLoading(true)
    try {
      const { token } = await login(password)
      localStorage.setItem('auth_token', token)
      onLogin()
    } catch {
      setError('Fel lösenord. Försök igen.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh" bgcolor="#f5f5f5">
      <Card sx={{ width: '100%', maxWidth: 360, mx: 2 }}>
        <CardContent sx={{ p: 4 }}>
          <Typography variant="h5" fontWeight={700} textAlign="center" gutterBottom>
            📅 Familjekalender AI
          </Typography>
          <Typography variant="body2" color="text.secondary" textAlign="center" mb={3}>
            Logga in för att fortsätta
          </Typography>
          <form onSubmit={handleSubmit}>
            <TextField
              fullWidth
              type="password"
              label="Lösenord"
              value={password}
              onChange={e => setPassword(e.target.value)}
              autoFocus
              sx={{ mb: 2 }}
            />
            {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={loading || !password}
              size="large"
            >
              {loading ? <CircularProgress size={24} color="inherit" /> : 'Logga in'}
            </Button>
          </form>
        </CardContent>
      </Card>
    </Box>
  )
}
