import { useParams, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { answerReviewQuestion, approveEvent, getFamilyMembers, getPendingReview, getReviewQuestions, rejectEvent, updateEvent } from '../api'
import {
  Typography, Stack, TextField, Button, CircularProgress,
  Alert, Paper, MenuItem, Select, FormControl, InputLabel, SelectChangeEvent, Divider
} from '@mui/material'
import { useEffect, useMemo, useState } from 'react'

function toDatetimeLocal(iso: string | null | undefined): string {
  if (!iso) return ''
  const d = new Date(iso)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

type QuestionAnswerState = {
  familyMemberName: string
  newInfo: string
}

export default function ReviewDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()
  const { data: events, isLoading } = useQuery({ queryKey: ['pending'], queryFn: getPendingReview })
  const { data: familyMembers = [] } = useQuery({ queryKey: ['familyMembers'], queryFn: getFamilyMembers })
  const { data: questions = [] } = useQuery({
    queryKey: ['reviewQuestions', id],
    queryFn: () => getReviewQuestions(id!),
    enabled: !!id
  })
  const evt = events?.find(e => e.id === id)

  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [location, setLocation] = useState('')
  const [familyMemberName, setFamilyMemberName] = useState('')
  const [startTime, setStartTime] = useState('')
  const [endTime, setEndTime] = useState('')
  const [questionAnswers, setQuestionAnswers] = useState<Record<number, QuestionAnswerState>>({})
  const [answeredIndexes, setAnsweredIndexes] = useState<number[]>([])

  useEffect(() => {
    if (!evt) return
    setTitle(evt.title ?? '')
    setDescription(evt.description ?? '')
    setLocation(evt.location ?? '')
    setFamilyMemberName(evt.familyMemberName ?? '')
    setStartTime(toDatetimeLocal(evt.startTime))
    setEndTime(toDatetimeLocal(evt.endTime))
  }, [evt])

  const familyMemberNames = useMemo(() => familyMembers.map(m => m.name), [familyMembers])

  const buildPatch = () => ({
    title,
    description,
    location,
    familyMemberName,
    startTime: startTime ? new Date(startTime).toISOString() : undefined,
    endTime: endTime ? new Date(endTime).toISOString() : undefined,
  })

  const saveMutation = useMutation({
    mutationFn: () => updateEvent(id!, buildPatch()),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['pending'] })
  })

  const approveMutation = useMutation({
    mutationFn: async () => {
      await updateEvent(id!, buildPatch())
      await approveEvent(id!)
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['pending'] }); navigate('/review') }
  })

  const rejectMutation = useMutation({
    mutationFn: () => rejectEvent(id!),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['pending'] }); navigate('/review') }
  })

  const questionMutation = useMutation({
    mutationFn: ({ answer }: { index: number; answer: QuestionAnswerState }) =>
      answerReviewQuestion(id!, answer.familyMemberName, answer.newInfo),
    onSuccess: (_, variables) => {
      setAnsweredIndexes(current => current.includes(variables.index) ? current : [...current, variables.index])
      qc.invalidateQueries({ queryKey: ['familyMembers'] })
    }
  })

  const setQuestionAnswer = (index: number, key: keyof QuestionAnswerState, value: string) => {
    setQuestionAnswers(current => ({
      ...current,
      [index]: {
        familyMemberName: current[index]?.familyMemberName ?? '',
        newInfo: current[index]?.newInfo ?? '',
        [key]: value
      }
    }))
  }

  if (isLoading) return <CircularProgress />
  if (!evt) return <Alert severity="error">Händelse hittades inte</Alert>

  const busy = saveMutation.isPending || approveMutation.isPending || rejectMutation.isPending

  return (
    <>
      <Typography variant="h4" gutterBottom>Granska händelse</Typography>
      <Stack spacing={3}>
        {questions.length > 0 && (
          <Paper sx={{ p: 3, maxWidth: 800 }}>
            <Stack spacing={2}>
              <Typography variant="h6">Profilfrågor</Typography>
              {questions.map((question, index) => {
                const answer = questionAnswers[index] ?? { familyMemberName: '', newInfo: '' }
                const answered = answeredIndexes.includes(index)

                return (
                  <Stack spacing={2} key={`${question.question}-${index}`}>
                    {index > 0 && <Divider />}
                    <Typography fontWeight={600}>{question.question}</Typography>
                    <Typography color="text.secondary">{question.context}</Typography>
                    <FormControl fullWidth>
                      <InputLabel>Familjemedlem</InputLabel>
                      <Select
                        value={answer.familyMemberName}
                        label="Familjemedlem"
                        onChange={(e: SelectChangeEvent) => setQuestionAnswer(index, 'familyMemberName', e.target.value)}
                      >
                        {familyMemberNames.map(name => <MenuItem key={name} value={name}>{name}</MenuItem>)}
                      </Select>
                    </FormControl>
                    <TextField
                      label="Ny profilinformation"
                      value={answer.newInfo}
                      onChange={e => setQuestionAnswer(index, 'newInfo', e.target.value)}
                      fullWidth
                    />
                    {answered && <Alert severity="success">Svaret sparades i profilen.</Alert>}
                    {questionMutation.isError && questionMutation.variables?.index === index && (
                      <Alert severity="error">Det gick inte att spara svaret.</Alert>
                    )}
                    <Button
                      variant="outlined"
                      onClick={() => questionMutation.mutate({ index, answer })}
                      disabled={questionMutation.isPending || !answer.familyMemberName || !answer.newInfo}
                    >
                      Spara profilsvar
                    </Button>
                  </Stack>
                )
              })}
            </Stack>
          </Paper>
        )}

        <Paper sx={{ p: 3, maxWidth: 600 }}>
          <Stack spacing={2}>
            <TextField label="Titel" value={title} onChange={e => setTitle(e.target.value)} fullWidth />

            <FormControl fullWidth>
              <InputLabel>Familjemedlem</InputLabel>
              <Select
                value={familyMemberNames.includes(familyMemberName) ? familyMemberName : ''}
                label="Familjemedlem"
                onChange={(e: SelectChangeEvent) => setFamilyMemberName(e.target.value)}
                displayEmpty
              >
                <MenuItem value=""><em>Ingen specifik familjemedlem</em></MenuItem>
                {familyMemberNames.map(name => <MenuItem key={name} value={name}>{name}</MenuItem>)}
              </Select>
            </FormControl>

            <TextField
              label="Starttid"
              type="datetime-local"
              value={startTime}
              onChange={e => setStartTime(e.target.value)}
              fullWidth
              InputLabelProps={{ shrink: true }}
            />
            <TextField
              label="Sluttid"
              type="datetime-local"
              value={endTime}
              onChange={e => setEndTime(e.target.value)}
              fullWidth
              InputLabelProps={{ shrink: true }}
            />

            <TextField label="Plats" value={location} onChange={e => setLocation(e.target.value)} fullWidth />

            <TextField
              label="Beskrivning"
              value={description}
              onChange={e => setDescription(e.target.value)}
              fullWidth
              multiline
              rows={3}
            />

            {(saveMutation.isError || approveMutation.isError) && (
              <Alert severity="error">Något gick fel, försök igen.</Alert>
            )}
            {saveMutation.isSuccess && (
              <Alert severity="success">Ändringar sparade.</Alert>
            )}

            <Stack direction="row" spacing={2} flexWrap="wrap">
              <Button variant="outlined" onClick={() => saveMutation.mutate()} disabled={busy}>
                Spara
              </Button>
              <Button variant="contained" color="success" onClick={() => approveMutation.mutate()} disabled={busy}>
                ✅ Spara &amp; godkänn
              </Button>
              <Button variant="outlined" color="error" onClick={() => rejectMutation.mutate()} disabled={busy}>
                ❌ Avvisa
              </Button>
            </Stack>
          </Stack>
        </Paper>
      </Stack>
    </>
  )
}
