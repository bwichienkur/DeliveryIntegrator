import { useEffect, useMemo, useState } from 'react'
import './App.css'

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5080'

type CatalogItem = {
  id: string
  key: string
  displayName: string
  dataType: string
  isPii: boolean
}

type DeliveryRun = {
  id: string
  institutionId: string
  status: string
  correlationId: string
}

function App() {
  const [catalog, setCatalog] = useState<CatalogItem[]>([])
  const [search, setSearch] = useState('')
  const [selectedField, setSelectedField] = useState('')
  const [runs, setRuns] = useState<DeliveryRun[]>([])
  const [institutionId, setInstitutionId] = useState('11111111-1111-1111-1111-111111111111')

  useEffect(() => {
    void loadCatalog()
    void loadRuns()
  }, [institutionId])

  async function loadCatalog() {
    const res = await fetch(`${API_BASE}/api/v1/field-catalog?institutionId=${institutionId}&search=${encodeURIComponent(search)}`)
    const data = await res.json()
    setCatalog(data.items ?? [])
  }

  async function loadRuns() {
    const res = await fetch(`${API_BASE}/api/v1/delivery-runs?institutionId=${institutionId}`)
    setRuns(await res.json())
  }

  const filtered = useMemo(
    () => catalog.filter((c) => !search || c.key.includes(search) || c.displayName.includes(search)),
    [catalog, search]
  )

  return (
    <div className="layout">
      <header>
        <h1>Lead Delivery Admin</h1>
        <p>MVP: field catalog picker, runs, and failures by institution.</p>
      </header>

      <section>
        <h2>Field picker</h2>
        <div className="row">
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search fields" />
          <button onClick={() => void loadCatalog()}>Refresh catalog</button>
        </div>
        <div className="row">
          <select value={selectedField} onChange={(e) => setSelectedField(e.target.value)}>
            <option value="">Select source field…</option>
            {filtered.map((f) => (
              <option key={f.id} value={f.key}>
                {f.key} ({f.dataType}){f.isPii ? ' · PII' : ''}
              </option>
            ))}
          </select>
        </div>
        {selectedField && <p className="hint">Mapping source: <code>{selectedField}</code></p>}
      </section>

      <section>
        <h2>Delivery runs</h2>
        <label>
          Institution Id{' '}
          <input value={institutionId} onChange={(e) => setInstitutionId(e.target.value)} style={{ width: 360 }} />
        </label>
        <table>
          <thead>
            <tr>
              <th>Run</th>
              <th>Status</th>
              <th>Correlation</th>
            </tr>
          </thead>
          <tbody>
            {runs.map((r) => (
              <tr key={r.id}>
                <td>{r.id}</td>
                <td>{r.status}</td>
                <td>{r.correlationId}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  )
}

export default App