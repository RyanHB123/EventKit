import { useEffect, useRef, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { ArrowDownLeft, ArrowRight, ArrowUpRight, CalendarDays, Check, CheckCircle2, ChevronRight, ClipboardList, Clock3, Headphones, LayoutDashboard, LogOut, Menu, Minus, Package, Pencil, Plus, Search, ShieldCheck, ShoppingBag, SlidersHorizontal, Sparkles, Users, Wrench, X } from 'lucide-react'
import { api } from './api'
import type { Booking, BookingInput, Equipment, EquipmentInput, Session } from './api'
import EquipmentArt from './EquipmentArt'
import './App.css'

const categories = ['All equipment', 'Audio', 'Lighting', 'Furniture', 'Outdoor', 'Presentation']
const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/London', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date())
const dateLabel = (date: string, options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short' }) => new Date(`${date}T12:00:00`).toLocaleDateString('en-GB', options)
const totalItems = (booking: Booking) => booking.items.reduce((sum, i) => sum + i.quantity, 0)
const statusLabel = (b: Booking) => b.isOverdue ? 'Overdue' : ({ Confirmed: 'Reserved', CheckedOut: 'On loan', Completed: 'Returned', Cancelled: 'Cancelled' })[b.status]
const message = (error: unknown) => error instanceof Error ? error.message : 'Something went wrong. Please try again.'

function Modal({ title, subtitle, children, onClose, wide = false }: { title: string; subtitle?: string; children: ReactNode; onClose: () => void; wide?: boolean }) {
  const dialog = useRef<HTMLDialogElement>(null)
  useEffect(() => { dialog.current?.showModal() }, [])
  return <dialog ref={dialog} className={`modal ${wide ? 'wide' : ''}`} onCancel={onClose} aria-labelledby="modal-title">
    <div className="modal-head"><div><h2 id="modal-title">{title}</h2>{subtitle && <p>{subtitle}</p>}</div><button className="icon-button" onClick={onClose} aria-label="Close dialog"><X size={20} /></button></div>
    {children}
  </dialog>
}

function Login({ session, onLogin }: { session: Session; onLogin: () => Promise<void> }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function login(e?: FormEvent, demo = false) {
    e?.preventDefault(); setBusy(true); setError('')
    try { await api.login(demo ? 'admin@eventkit.local' : email, demo ? 'EventKit-demo-2026!' : password); await onLogin() }
    catch (err) { setError(message(err)) } finally { setBusy(false) }
  }
  return <div className="login-page"><div className="login-story">
    <div className="brand"><span className="brand-mark"><Package size={24} /></span>EventKit<span className="brand-dot">.</span></div>
    <div className="login-copy"><span className="eyebrow">MORE EVENTS. LESS ADMIN.</span><h1>Great events<br />start with<br /><em>the right kit.</em></h1><p>A little organisation goes a long way. Keep your equipment, bookings and community in sync.</p>
      <div className="login-pills"><span><Headphones size={16} /> Sound</span><span><Sparkles size={16} /> Lighting</span><span><Users size={16} /> Gathering</span></div>
    </div><div className="login-bottom"><span className="status-dot" /> Equipment for every occasion</div>
  </div><div className="login-panel"><div className="login-form"><span className="mini-logo"><Package size={25} /></span><h2>Welcome to EventKit</h2><p>Sign in to your equipment workspace.</p>
    <form onSubmit={e => void login(e)}><label>Email address<input type="email" autoComplete="username" value={email} onChange={e => setEmail(e.target.value)} placeholder="you@yourvenue.co.uk" required /></label>
    <label>Password<input type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required placeholder="Enter your password" /></label>
    {error && <p className="form-error" role="alert">{error}</p>}<button className="button primary full" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}<ArrowRight size={17} /></button></form>
    {session.demo && <div className="demo-box"><span className="eyebrow">TAKE A LOOK AROUND</span><p>This local demo contains fictional equipment and bookings.</p><button className="button secondary full" disabled={busy} onClick={() => void login(undefined, true)}>Open demo workspace <ArrowUpRight size={16} /></button></div>}
    <div className="login-secure"><ShieldCheck size={15} /> Your community, in good hands.</div>
  </div></div></div>
}

function BookingForm({ equipment, selected, dates, onDates, onClose, onSaved }: {
  equipment: Equipment[]; selected: Record<string, number>; dates: { start: string; end: string };
  onDates: (dates: { start: string; end: string }) => void; onClose: () => void; onSaved: () => Promise<void>;
}) {
  const [items, setItems] = useState(selected)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [fields, setFields] = useState({ eventName: '', borrowerName: '', email: '', phone: '', notes: '' })
  const field = (key: keyof typeof fields, value: string) => setFields({ ...fields, [key]: value })
  const units = Object.values(items).reduce((sum, q) => sum + q, 0)
  async function submit(e: FormEvent) {
    e.preventDefault(); setError(''); setBusy(true)
    try {
      const input: BookingInput = { ...fields, startDate: dates.start, endDate: dates.end, items: Object.entries(items).filter(([, q]) => q > 0).map(([equipmentId, quantity]) => ({ equipmentId, quantity })) }
      await api.createBooking(input); await onSaved(); onClose()
    } catch (err) { setError(message(err)) } finally { setBusy(false) }
  }
  return <Modal title="Create a booking" subtitle="Bring the right equipment to your next event." onClose={onClose} wide>
    <form onSubmit={submit}><div className="booking-form-grid"><div className="form-section">
      <h3><span>01</span> Event details</h3><label>Event name<input autoFocus value={fields.eventName} onChange={e => field('eventName', e.target.value)} required maxLength={150} placeholder="e.g. Community summer fair" /></label>
      <div className="form-row"><label>Collection date<input aria-label="Booking collection date" type="date" min={today} value={dates.start} onChange={e => { if (e.target.value) onDates({ start: e.target.value, end: dates.end < e.target.value ? e.target.value : dates.end }) }} required /></label>
        <label>Return date<input aria-label="Booking return date" type="date" min={dates.start} value={dates.end} onChange={e => { if (e.target.value) onDates({ ...dates, end: e.target.value }) }} required /></label></div>
      <h3><span>02</span> Borrower details</h3><label>Full name<input value={fields.borrowerName} onChange={e => field('borrowerName', e.target.value)} required maxLength={100} placeholder="Who is collecting the equipment?" /></label>
      <label>Email address<input type="email" value={fields.email} onChange={e => field('email', e.target.value)} required maxLength={254} placeholder="borrower@example.com" /></label>
      <label>Phone <small>optional</small><input type="tel" value={fields.phone} onChange={e => field('phone', e.target.value)} maxLength={40} placeholder="Contact number" /></label>
      <label>Notes <small>optional</small><textarea value={fields.notes} onChange={e => field('notes', e.target.value)} maxLength={1000} rows={2} placeholder="Collection arrangements or anything useful…" /></label>
    </div><div className="item-selection"><h3><span>03</span> Choose equipment</h3><p className="muted">Availability is checked for your full date range.</p><div className="item-selection-list">
      {equipment.map(item => <div className="selection-item" key={item.id}><div><strong>{item.name}</strong><small>{item.availableQuantity} available</small></div>
        <div className="stepper"><button type="button" aria-label={`Remove one ${item.name}`} disabled={!items[item.id]} onClick={() => setItems({ ...items, [item.id]: Math.max(0, (items[item.id] ?? 0) - 1) })}><Minus size={13} /></button>
          <input type="number" aria-label={`${item.name} quantity`} min={0} max={item.availableQuantity} value={items[item.id] ?? 0} onChange={e => setItems({ ...items, [item.id]: Math.max(0, Math.min(item.availableQuantity, Number(e.target.value))) })} />
          <button type="button" aria-label={`Add one ${item.name}`} disabled={(items[item.id] ?? 0) >= item.availableQuantity} onClick={() => setItems({ ...items, [item.id]: (items[item.id] ?? 0) + 1 })}><Plus size={13} /></button></div>
      </div>)}
    </div><div className="booking-total"><ShoppingBag size={18} /><span>{units} units selected</span><strong>{Object.values(items).filter(q => q > 0).length} item types</strong></div></div></div>
    {error && <div className="form-error" role="alert">{error}</div>}<div className="modal-footer"><span><ShieldCheck size={15} /> Stock is reserved when you confirm.</span><div><button type="button" className="button secondary" onClick={onClose}>Cancel</button><button className="button primary" disabled={busy || units === 0}>{busy ? 'Reserving…' : 'Confirm booking'}<ArrowRight size={16} /></button></div></div>
    </form>
  </Modal>
}

function EquipmentForm({ item, onClose, onSaved }: { item?: Equipment; onClose: () => void; onSaved: () => Promise<void> }) {
  const [input, setInput] = useState<EquipmentInput>(item ?? { name: '', category: 'Audio', description: '', location: 'Main store', totalQuantity: 1, maintenanceQuantity: 0 })
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  async function submit(e: FormEvent) {
    e.preventDefault(); setBusy(true); setError('')
    try { await api.saveEquipment(input, item?.id); await onSaved(); onClose() } catch (err) { setError(message(err)) } finally { setBusy(false) }
  }
  return <Modal title={item ? 'Manage equipment' : 'Add equipment'} subtitle="Keep your inventory ready for the next event." onClose={onClose}>
    <form onSubmit={submit} className="equipment-form"><label>Equipment name<input autoFocus value={input.name} required maxLength={100} onChange={e => setInput({ ...input, name: e.target.value })} /></label>
      <div className="form-row"><label>Category<select value={input.category} onChange={e => setInput({ ...input, category: e.target.value })}>{categories.slice(1).map(c => <option key={c}>{c}</option>)}</select></label><label>Storage location<input value={input.location} maxLength={100} onChange={e => setInput({ ...input, location: e.target.value })} /></label></div>
      <label>Description<textarea rows={3} value={input.description} maxLength={600} onChange={e => setInput({ ...input, description: e.target.value })} /></label>
      <div className="form-row"><label>Total stock<input type="number" min={1} max={10000} required value={input.totalQuantity} onChange={e => setInput({ ...input, totalQuantity: Number(e.target.value) })} /></label><label>In maintenance<input type="number" min={0} max={input.totalQuantity} required value={input.maintenanceQuantity} onChange={e => setInput({ ...input, maintenanceQuantity: Number(e.target.value) })} /></label></div>
      <p className="muted small">Equipment in maintenance cannot be booked. Existing reservations are protected when stock changes.</p>{error && <p className="form-error" role="alert">{error}</p>}
      <div className="modal-footer"><button type="button" className="button secondary" onClick={onClose}>Cancel</button><button className="button primary" disabled={busy}>{busy ? 'Saving…' : 'Save equipment'}<Check size={16} /></button></div>
    </form></Modal>
}

export default function App() {
  const [session, setSession] = useState<Session | null>(null)
  const [page, setPage] = useState<'Overview' | 'Equipment' | 'Bookings'>('Overview')
  const [equipment, setEquipment] = useState<Equipment[]>([])
  const [bookings, setBookings] = useState<Booking[]>([])
  const [dates, setDates] = useState({ start: today, end: today })
  const [search, setSearch] = useState(''); const [category, setCategory] = useState('All equipment')
  const [onlyAvailable, setOnlyAvailable] = useState(false); const [filter, setFilter] = useState('Active')
  const [selected, setSelected] = useState<Record<string, number>>({})
  const [bookingOpen, setBookingOpen] = useState(false); const [editEquipment, setEditEquipment] = useState<Equipment | 'new' | null>(null)
  const [detail, setDetail] = useState<Booking | null>(null)
  const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [toast, setToast] = useState('')
  const [busy, setBusy] = useState(false); const [sidebarOpen, setSidebarOpen] = useState(false)
  const refresh = async () => {
    const [stock, list] = await Promise.all([api.equipment(dates.start, dates.end), api.bookings()])
    setEquipment(stock); setBookings(list)
  }
  useEffect(() => { api.session().then(setSession).catch(err => setError(message(err))).finally(() => setLoading(false)) }, [])
  useEffect(() => {
    if (!session?.user) return
    let current = true
    Promise.all([api.equipment(dates.start, dates.end), api.bookings()]).then(([stock, list]) => { if (current) { setEquipment(stock); setBookings(list); setError('') } })
      .catch(err => { if (current) setError(message(err)) }).finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [session?.user, dates.start, dates.end])
  useEffect(() => { if (!toast) return; const timer = window.setTimeout(() => setToast(''), 4500); return () => clearTimeout(timer) }, [toast])
  const login = async () => { setSession(await api.session()) }
  const notify = (text: string) => setToast(text)
  const navigate = (next: typeof page) => { setPage(next); setSearch(''); setSidebarOpen(false) }
  const active = bookings.filter(b => b.status === 'Confirmed' || b.status === 'CheckedOut')
  const upcoming = [...active].sort((a, b) => a.startDate.localeCompare(b.startDate))
  const units = equipment.reduce((sum, e) => sum + e.totalQuantity, 0)
  const onLoan = bookings.filter(b => b.status === 'CheckedOut').reduce((sum, b) => sum + totalItems(b), 0)
  const maintenance = equipment.reduce((sum, e) => sum + e.maintenanceQuantity, 0)
  const selectedUnits = Object.values(selected).reduce((sum, n) => sum + n, 0)
  const stock = equipment.filter(e => (category === 'All equipment' || e.category === category) && (!onlyAvailable || e.availableQuantity > 0)
    && `${e.name} ${e.description} ${e.category}`.toLowerCase().includes(search.toLowerCase()))
  const filteredBookings = bookings.filter(b => (filter === 'All' || (filter === 'Active' && ['Confirmed', 'CheckedOut'].includes(b.status))
    || (filter === 'Returned' && b.status === 'Completed') || (filter === 'Cancelled' && b.status === 'Cancelled'))
    && `${b.reference} ${b.eventName} ${b.borrowerName} ${b.email}`.toLowerCase().includes(search.toLowerCase()))
  const add = (item: Equipment) => { setSelected({ ...selected, [item.id]: Math.min(item.availableQuantity, (selected[item.id] ?? 0) + 1) }); notify(`${item.name} added to your booking`) }
  async function transition(booking: Booking, action: string) {
    setBusy(true)
    try { await api.transition(booking.id, action); await refresh(); setDetail(null); notify(action === 'checkout' ? 'Equipment checked out' : action === 'return' ? 'Equipment returned to stock' : 'Booking cancelled; stock released') }
    catch (err) { notify(message(err)) } finally { setBusy(false) }
  }
  async function logout() { try { await api.logout(); setSession(await api.session()); setSelected({}); setEquipment([]); setBookings([]) } catch (err) { notify(message(err)) } }
  if (!session) return <div className="startup"><Package size={36} /><h2>Getting your workspace ready…</h2>{error && <><p role="alert">{error}</p><button className="button secondary" onClick={() => location.reload()}>Try again</button></>}</div>
  if (!session.user) return <Login session={session} onLogin={login} />
  return <div className="app-shell">
    {sidebarOpen && <button className="mobile-scrim" aria-label="Close navigation" onClick={() => setSidebarOpen(false)} />}
    <aside className={`sidebar ${sidebarOpen ? 'open' : ''}`}><div className="brand"><span className="brand-mark"><Package size={24} /></span>EventKit<span className="brand-dot">.</span></div>
      <div className="workspace"><span className="workspace-icon"><Users size={19} /></span><div><strong>Community workspace</strong><small>Event equipment lending</small></div></div>
      <span className="nav-label">WORKSPACE</span><nav>{([{ name: 'Overview', icon: LayoutDashboard }, { name: 'Equipment', icon: Package }, { name: 'Bookings', icon: CalendarDays }] as const).map(n => <button className={page === n.name ? 'active' : ''} key={n.name} onClick={() => navigate(n.name)}><n.icon size={19} /><span>{n.name}</span>{n.name === 'Bookings' && <small>{active.length}</small>}</button>)}</nav>
      <div className="sidebar-note"><span className="note-icon"><Sparkles size={21} /></span><h4>Good kit. Great gatherings.</h4><p>A shared collection.<br />A whole lot of possibilities.</p><button onClick={() => navigate('Equipment')}>Explore your equipment <ArrowRight size={14} /></button></div>
      <div className="sidebar-footer"><span className="avatar">EK</span><div><strong>{session.user.name}</strong><small>Staff workspace</small></div><button aria-label="Sign out" title="Sign out" onClick={() => void logout()}><LogOut size={17} /></button></div>
    </aside>
    <div className="main-shell"><header className="topbar"><div className="breadcrumbs"><button className="icon-button menu-toggle" aria-label="Open navigation" onClick={() => setSidebarOpen(true)}><Menu size={20} /></button><span>Workspace</span><ChevronRight size={14} /><strong>{page}</strong></div><div className="topbar-right"><span className="today"><CalendarDays size={15} />{dateLabel(today, { day: 'numeric', month: 'short', year: 'numeric' })}</span><span className="topbar-divider" /><span className="avatar small-avatar">EK</span></div></header>
      <main><div className="page-heading"><div><span className="eyebrow">YOUR COMMUNITY, WELL EQUIPPED</span><h1>{page === 'Overview' ? 'A little planning. A great event.' : page === 'Equipment' ? 'Equipment for every occasion.' : 'Every booking, in one place.'}</h1><p>{page === 'Overview' ? 'Here’s what’s happening in your equipment collection.' : page === 'Equipment' ? 'Find the right kit, check availability and get your event started.' : 'From the first reservation to the final return. Keep things moving.'}</p></div><button className="button primary" onClick={() => setBookingOpen(true)}><Plus size={18} />New booking</button></div>
      {error && <div className="error-banner" role="alert">{error}<button onClick={() => void refresh().then(() => setError('')).catch(err => setError(message(err)))}>Retry</button></div>}
      {loading && <div className="loading-line" role="status" aria-label="Loading equipment" />}
      {page === 'Overview' && <>
        <div className="stats-grid">{[
          { name: 'Equipment in stock', value: units, detail: `${equipment.length} equipment types`, icon: Package, tone: 'sage' },
          { name: 'Active bookings', value: active.length, detail: 'Reserved and on loan', icon: CalendarDays, tone: 'peach' },
          { name: 'Currently on loan', value: onLoan, detail: 'Units out making events happen', icon: ArrowUpRight, tone: 'blue' },
          { name: 'In maintenance', value: maintenance, detail: 'Getting ready for their next event', icon: Wrench, tone: 'yellow' },
        ].map(s => <div className="stat-card" key={s.name}><div className="stat-top"><span>{s.name}</span><span className={`stat-icon ${s.tone}`}><s.icon size={19} /></span></div><strong>{s.value}<span>{s.name === 'Active bookings' ? 'bookings' : 'units'}</span></strong><small>{s.detail}</small></div>)}</div>
        <div className="overview-grid"><section className="panel"><div className="section-head"><div><h2>Upcoming & on loan <span className="count-pill">{active.length}</span></h2><p>The next events in your community.</p></div><button className="text-button" onClick={() => navigate('Bookings')}>View all <ArrowRight size={15} /></button></div>
          <div className="upcoming-list">{upcoming.slice(0, 4).map(b => <button className="upcoming-item" key={b.id} onClick={() => setDetail(b)}><div className="date-tile"><small>{dateLabel(b.startDate, { month: 'short' }).toUpperCase()}</small><strong>{dateLabel(b.startDate, { day: 'numeric' })}</strong></div><div className="event-info"><strong>{b.eventName}</strong><small>{b.borrowerName}<span>·</span>{totalItems(b)} units</small></div><span className={`badge ${b.isOverdue ? 'overdue' : b.status.toLowerCase()}`}>{statusLabel(b)}</span><ChevronRight size={16} /></button>)}{!active.length && <div className="empty-state"><CalendarDays /><h3>A clear calendar</h3><p>Create a booking to get things started.</p></div>}</div>
        </section><section className="getting-started"><div className="little-orbit"><Package size={34} /><span><Check size={14} /></span></div><span className="eyebrow">READY WHEN YOU ARE</span><h2>One booking.<br />Everything you need.</h2><p>Speakers, seats, a little atmosphere. Put your event kit together in a few clicks.</p><button className="button secondary" onClick={() => setBookingOpen(true)}>Plan an event <ArrowRight size={16} /></button><div className="decor-circle circle-one" /><div className="decor-circle circle-two" /></section></div>
        <div className="section-head catalogue-heading"><div><h2>Meet your equipment</h2><p>A few favourites from your shared collection.</p></div><button className="text-button" onClick={() => navigate('Equipment')}>Browse catalogue <ArrowRight size={16} /></button></div>
        <div className="equipment-grid overview-cards">{equipment.filter(e => ['Powered PA speaker', 'Wireless microphone', 'Folding chair', 'LED stage light'].includes(e.name)).map(item => <EquipmentCard key={item.id} item={item} quantity={selected[item.id] ?? 0} onAdd={() => add(item)} onEdit={() => setEditEquipment(item)} />)}</div>
      </>}
      {page === 'Equipment' && <>
        <div className="catalogue-toolbar"><label className="search-field"><Search size={18} /><input aria-label="Search equipment" placeholder="Search your equipment…" value={search} onChange={e => setSearch(e.target.value)} /></label><div className="date-range"><CalendarDays size={16} /><input aria-label="Availability start date" type="date" min={today} value={dates.start} onChange={e => { if (e.target.value) setDates({ start: e.target.value, end: dates.end < e.target.value ? e.target.value : dates.end }) }} /><span>—</span><input aria-label="Availability end date" type="date" min={dates.start} value={dates.end} onChange={e => { if (e.target.value) setDates({ ...dates, end: e.target.value }) }} /></div><button className="button secondary" onClick={() => setEditEquipment('new')}><Plus size={16} />Add equipment</button></div>
        <div className="catalogue-filters"><div className="category-tabs">{categories.map(c => <button className={category === c ? 'selected' : ''} key={c} onClick={() => setCategory(c)}>{c}{c === 'All equipment' && <span>{equipment.length}</span>}</button>)}</div><label className="availability-toggle"><input type="checkbox" checked={onlyAvailable} onChange={e => setOnlyAvailable(e.target.checked)} />Available only</label></div>
        <div className="results-meta"><span>{stock.length} equipment types <span className="meta-dot">·</span> Availability for {dateLabel(dates.start)}{dates.end !== dates.start && ` – ${dateLabel(dates.end)}`}</span><span><SlidersHorizontal size={14} /> Name A–Z</span></div>
        <div className="equipment-grid">{stock.map(item => <EquipmentCard key={item.id} item={item} quantity={selected[item.id] ?? 0} onAdd={() => add(item)} onEdit={() => setEditEquipment(item)} />)}</div>
        {!stock.length && !loading && <div className="empty-state"><Package /><h3>No equipment found</h3><p>Try another search or category, or add your first item.</p><button className="button secondary" onClick={() => { setSearch(''); setCategory('All equipment'); setOnlyAvailable(false) }}>Clear filters</button></div>}
      </>}
      {page === 'Bookings' && <section className="panel bookings-panel"><div className="bookings-toolbar"><div className="booking-tabs">{['Active', 'All', 'Returned', 'Cancelled'].map(f => <button key={f} className={filter === f ? 'selected' : ''} onClick={() => setFilter(f)}>{f}</button>)}</div><label className="search-field"><Search size={17} /><input aria-label="Search bookings" placeholder="Search events or borrowers…" value={search} onChange={e => setSearch(e.target.value)} /></label></div>
        <div className="table-scroll"><table><thead><tr><th>Event & borrower</th><th>Dates</th><th>Equipment</th><th>Status</th><th><span className="sr-only">Details</span></th></tr></thead><tbody>{filteredBookings.map(b => <tr key={b.id}><td><button className="booking-name" onClick={() => setDetail(b)}>{b.eventName}</button><small>{b.borrowerName}<span> · </span>{b.reference}</small></td><td><strong>{dateLabel(b.startDate)}{b.endDate !== b.startDate && ` – ${dateLabel(b.endDate)}`}</strong><small>{dateLabel(b.startDate, { year: 'numeric' })}</small></td><td><strong>{totalItems(b)} units</strong><small>{b.items.length} equipment types</small></td><td><span className={`badge ${b.isOverdue ? 'overdue' : b.status.toLowerCase()}`}>{statusLabel(b)}</span></td><td><button className="icon-button" aria-label={`View ${b.eventName}`} onClick={() => setDetail(b)}><ChevronRight size={18} /></button></td></tr>)}</tbody></table></div>
        {!filteredBookings.length && <div className="empty-state"><ClipboardList /><h3>No bookings here yet</h3><p>{search ? 'Try a different search.' : 'Your bookings will appear here.'}</p></div>}
        <div className="table-footer">{filteredBookings.length} bookings<span><ShieldCheck size={14} /> Stock checked for every reservation</span></div>
      </section>}
      <footer className="page-footer"><span>EventKit<span className="brand-dot">.</span> <span>Equipment for every occasion.</span></span><span><span className="status-dot" />Your collection, connected.</span></footer>
      </main>
    </div>
    {selectedUnits > 0 && !bookingOpen && <div className="booking-basket"><ShoppingBag size={20} /><div><strong>{selectedUnits} units in your booking</strong><small>{Object.values(selected).filter(n => n > 0).length} equipment types selected</small></div><button className="text-button" onClick={() => setSelected({})}>Clear</button><button className="button primary" onClick={() => setBookingOpen(true)}>Create booking <ArrowRight size={16} /></button></div>}
    {toast && <div className="toast" role="status"><CheckCircle2 size={18} /><span>{toast}</span><button aria-label="Dismiss notification" onClick={() => setToast('')}><X size={15} /></button></div>}
    {bookingOpen && <BookingForm equipment={equipment} selected={selected} dates={dates} onDates={setDates} onClose={() => setBookingOpen(false)} onSaved={async () => { setSelected({}); await refresh(); notify('Booking confirmed. Your equipment is reserved.'); setPage('Bookings') }} />}
    {editEquipment && <EquipmentForm item={editEquipment === 'new' ? undefined : editEquipment} onClose={() => setEditEquipment(null)} onSaved={async () => { await refresh(); notify('Equipment saved') }} />}
    {detail && <Modal title={detail.eventName} subtitle={`${detail.reference} · ${detail.borrowerName}`} onClose={() => setDetail(null)}>
      <div className="booking-detail"><div className="detail-status"><span className={`badge ${detail.isOverdue ? 'overdue' : detail.status.toLowerCase()}`}>{statusLabel(detail)}</span><span><CalendarDays size={16} />{dateLabel(detail.startDate)} – {dateLabel(detail.endDate)}</span></div>
        <div className="detail-person"><span className="avatar">{detail.borrowerName.split(' ').map(n => n[0]).join('').slice(0, 2)}</span><div><strong>{detail.borrowerName}</strong><a href={`mailto:${detail.email}`}>{detail.email}</a>{detail.phone && <small>{detail.phone}</small>}</div></div>
        <h3>Reserved equipment</h3><div className="detail-items">{detail.items.map(i => <div key={i.equipmentId}><span><Package size={16} />{i.name}</span><strong>× {i.quantity}</strong></div>)}</div>
        {detail.notes && <div className="detail-notes"><strong>Notes</strong><p>{detail.notes}</p></div>}
        {detail.isOverdue && <p className="form-error"><Clock3 size={15} />This loan is overdue. Its equipment remains unavailable until returned.</p>}
        <div className="modal-footer">{detail.status === 'Confirmed' && <><button className="button danger" disabled={busy} onClick={() => void transition(detail, 'cancel')}>Cancel booking</button><button className="button primary" disabled={busy || detail.startDate > today || detail.endDate < today} onClick={() => void transition(detail, 'checkout')}><ArrowUpRight size={17} />{busy ? 'Updating…' : 'Check out equipment'}</button></>}
          {detail.status === 'CheckedOut' && <button className="button primary" disabled={busy} onClick={() => void transition(detail, 'return')}><ArrowDownLeft size={17} />{busy ? 'Updating…' : 'Mark everything returned'}</button>}
          {['Completed', 'Cancelled'].includes(detail.status) && <button className="button secondary" onClick={() => setDetail(null)}>Close</button>}</div>
      </div></Modal>}
  </div>
}

function EquipmentCard({ item, quantity, onAdd, onEdit }: { item: Equipment; quantity: number; onAdd: () => void; onEdit: () => void }) {
  return <article className="equipment-card"><div className="card-art-wrap"><EquipmentArt name={item.name} category={item.category} /><span className="category-label">{item.category}</span><button className="card-edit" aria-label={`Manage ${item.name}`} title="Manage stock" onClick={onEdit}><Pencil size={14} /></button></div>
    <div className="equipment-card-body"><h3>{item.name}</h3><p>{item.description}</p><div className="stock-line"><span className={`stock-dot ${item.availableQuantity === 0 ? 'unavailable' : ''}`} /><strong>{item.availableQuantity} available</strong><span>of {item.totalQuantity}</span>{item.maintenanceQuantity > 0 && <span title={`${item.maintenanceQuantity} units in maintenance`} className="maintenance-indicator"><Wrench size={12} />{item.maintenanceQuantity}</span>}</div>
    <div className="card-bottom"><small>{item.location.split(' · ')[0]}</small><button className={`add-button ${quantity ? 'added' : ''}`} disabled={quantity >= item.availableQuantity} onClick={onAdd}>{quantity ? <Check size={14} /> : <Plus size={14} />}{quantity ? `${quantity} selected` : 'Add to booking'}</button></div></div>
  </article>
}
