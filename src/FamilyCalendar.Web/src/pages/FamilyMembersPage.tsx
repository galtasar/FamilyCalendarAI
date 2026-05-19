import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Card, CardContent, CircularProgress, Grid, Stack, TextField, Typography } from '@mui/material'
import { FamilyMember, getFamilyMembers, updateFamilyMember } from '../api'

export default function FamilyMembersPage() {
  const qc = useQueryClient()
  const { data: familyMembers, isLoading, isError } = useQuery({ queryKey: ['familyMembers'], queryFn: getFamilyMembers })
  const [drafts, setDrafts] = useState<Record<string, string>>({})
  const [savedId, setSavedId] = useState<string | null>(null)

  useEffect(() => {
    if (!familyMembers) return
    setDrafts(Object.fromEntries(familyMembers.map(m => [m.id, m.description ?? ''])))
  }, [familyMembers])

  const saveMutation = useMutation({
    mutationFn: ({ id, description }: { id: string; description: string }) => updateFamilyMember(id, { description }),
    onSuccess: updated => {
      setSavedId(updated.id)
      qc.setQueryData<FamilyMember[]>(['familyMembers'], current =>
        current?.map(m => m.id === updated.id ? updated : m) ?? [updated])
    }
  })

  if (isLoading) return <CircularProgress />
  if (isError || !familyMembers) return <Alert severity="error">Kunde inte ladda familjemedlemmar.</Alert>

  return (
    <>
      <Typography variant="h4" gutterBottom>Familjemedlemmar</Typography>
      <Grid container spacing={3}>
        {familyMembers.map(member => {
          const description = drafts[member.id] ?? member.description ?? ''
          const isSaving = saveMutation.isPending && saveMutation.variables?.id === member.id

          return (
            <Grid item xs={12} md={6} key={member.id}>
              <Card>
                <CardContent>
                  <Stack spacing={2}>
                    <Typography variant="h6">{member.name}</Typography>
                    <TextField
                      label="Beskrivning"
                      value={description}
                      onChange={e => {
                        setSavedId(null)
                        setDrafts(current => ({ ...current, [member.id]: e.target.value }))
                      }}
                      fullWidth
                      multiline
                      minRows={4}
                      placeholder="Beskriv familjemedlemmen — skola, aktiviteter, föreningar etc."
                    />
                    {savedId === member.id && <Alert severity="success">Profil sparad.</Alert>}
                    {saveMutation.isError && saveMutation.variables?.id === member.id && (
                      <Alert severity="error">Det gick inte att spara profilen.</Alert>
                    )}
                    <Button
                      variant="contained"
                      onClick={() => saveMutation.mutate({ id: member.id, description })}
                      disabled={isSaving}
                    >
                      {isSaving ? 'Sparar...' : 'Spara'}
                    </Button>
                  </Stack>
                </CardContent>
              </Card>
            </Grid>
          )
        })}
      </Grid>
    </>
  )
}
