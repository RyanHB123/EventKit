export type Equipment = {
  id: string; name: string; category: string; description: string; location: string;
  totalQuantity: number; maintenanceQuantity: number; availableQuantity: number;
}
export type BookingItem = { equipmentId: string; name: string; category: string; quantity: number }
export type Booking = {
  id: string; reference: string; eventName: string; borrowerName: string; email: string; phone: string | null;
  startDate: string; endDate: string; status: 'Confirmed' | 'CheckedOut' | 'Completed' | 'Cancelled';
  notes: string | null; isOverdue: boolean; items: BookingItem[];
}
export type Session = { user: { name: string; email: string } | null; csrfToken: string; demo: boolean }
export type BookingInput = {
  eventName: string; borrowerName: string; email: string; phone: string; startDate: string; endDate: string;
  notes: string; items: { equipmentId: string; quantity: number }[];
}
export type EquipmentInput = Pick<Equipment, 'name' | 'category' | 'description' | 'location' | 'totalQuantity' | 'maintenanceQuantity'>

let csrfToken = ''
async function request<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const response = await fetch(`/api${path}`, {
    method, credentials: 'same-origin',
    headers: { 'Content-Type': 'application/json', ...(method !== 'GET' ? { 'X-CSRF-TOKEN': csrfToken } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { title?: string } | null
    throw new Error(response.status === 429 ? 'Too many sign-in attempts. Wait a minute and try again.'
      : problem?.title ?? (response.status === 401 ? 'Please sign in to continue.' : 'Something went wrong. Please try again.'))
  }
  return response.status === 204 ? undefined as T : response.json() as Promise<T>
}
export const api = {
  session: async () => { const result = await request<Session>('/auth/session'); csrfToken = result.csrfToken; return result },
  login: (email: string, password: string) => request('/auth/login', 'POST', { email, password }),
  logout: () => request('/auth/logout', 'POST'),
  equipment: (start: string, end: string) => request<Equipment[]>(`/equipment?start=${start}&end=${end}`),
  bookings: () => request<Booking[]>('/bookings'),
  createBooking: (input: BookingInput) => request<Booking>('/bookings', 'POST', input),
  transition: (id: string, action: string) => request<Booking>(`/bookings/${id}/${action}`, 'POST'),
  saveEquipment: (input: EquipmentInput, id?: string) => request<Equipment>(`/equipment${id ? `/${id}` : ''}`, id ? 'PUT' : 'POST', input),
}
