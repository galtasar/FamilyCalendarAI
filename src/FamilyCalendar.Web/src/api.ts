import axios from 'axios'

const api = axios.create({ baseURL: '' })

export interface EmailSummary {
  id: string
  sender: string
  subject: string
  receivedAt: string
  processedAt: string | null
  classification: string
  confidence: number | null
}

export interface CalendarEvent {
  id: string
  emailId: string
  familyMemberName: string
  title: string
  description: string | null
  startTime: string
  endTime: string | null
  location: string | null
  calendarProvider: string
  calendarEventId: string | null
  reviewQuestionsJson?: string | null
  status: string
  needsReview: boolean
  createdAt: string
}

export interface FamilyMember {
  id: string
  name: string
  description: string | null
}

export interface ReviewQuestion {
  question: string
  context: string
}

export const getEmails = () => api.get<EmailSummary[]>('/api/emails').then(r => r.data)
export const getEmail = (id: string) => api.get<EmailSummary & { events: CalendarEvent[] }>(`/api/emails/${id}`).then(r => r.data)
export const getEvents = (params?: { familyMemberName?: string; from?: string; to?: string }) => api.get<CalendarEvent[]>('/api/events', { params }).then(r => r.data)
export const getPendingReview = () => api.get<CalendarEvent[]>('/api/events/pending-review').then(r => r.data)
export const approveEvent = (id: string) => api.post(`/api/events/${id}/approve`).then(r => r.data)
export const rejectEvent = (id: string) => api.post(`/api/events/${id}/reject`).then(r => r.data)
export const updateEvent = (id: string, data: Partial<CalendarEvent>) => api.patch(`/api/events/${id}`, data).then(r => r.data)
export const getFamilyMembers = () => api.get<FamilyMember[]>('/api/familymembers').then(r => r.data)
export const updateFamilyMember = (id: string, data: { description?: string }) => api.patch<FamilyMember>(`/api/familymembers/${id}`, data).then(r => r.data)
export const syncEmails = () => api.post('/api/sync-emails').then(r => r.data)
export const getReviewQuestions = (eventId: string) => api.get<ReviewQuestion[]>(`/api/events/${eventId}/review-questions`).then(r => r.data)
export const answerReviewQuestion = (eventId: string, familyMemberName: string, newInfo: string) =>
  api.post(`/api/events/${eventId}/answer-question`, { familyMemberName, newInfo }).then(r => r.data)
