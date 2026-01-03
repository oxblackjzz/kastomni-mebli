import { useState, useEffect } from 'react'
import { api } from '../api'
import { categories } from '../data'
import './Admin.css'

export default function Admin({ onLogout }) {
  const [activeTab, setActiveTab] = useState('portfolio')
  const [portfolio, setPortfolio] = useState([])
  const [reviews, setReviews] = useState([])
  const [contacts, setContacts] = useState([])
  const [loading, setLoading] = useState(true)
  const [editItem, setEditItem] = useState(null)
  const [showForm, setShowForm] = useState(false)

  useEffect(() => {
    loadData()
  }, [activeTab])

  const loadData = async () => {
    setLoading(true)
    try {
      if (activeTab === 'portfolio') {
        const data = await api.getPortfolio()
        setPortfolio(data)
      } else if (activeTab === 'reviews') {
 const data = await api.getReviewsAdmin()
        setReviews(data)
      } else if (activeTab === 'contacts') {
        const data = await api.getContacts()
        setContacts(data)
      }
    } catch (err) {
      console.error('Error loading data:', err)
    }
    setLoading(false)
  }

  const handleDelete = async (type, id) => {
    if (!confirm('Vy vpevneni?')) return
    
    try {
 if (type === 'portfolio') {
await api.deletePortfolioItem(id)
      } else if (type === 'reviews') {
        await api.deleteReview(id)
      } else if (type === 'contacts') {
   await api.deleteContact(id)
    }
   loadData()
    } catch (err) {
      console.error('Error deleting:', err)
    }
  }

  const handleMarkProcessed = async (id) => {
    try {
      await api.markContactProcessed(id)
      loadData()
    } catch (err) {
      console.error('Error:', err)
    }
  }

  return (
    <div className="admin">
      <header className="admin-header">
<h1>Admin Panel</h1>
    <button onClick={onLogout} className="btn-logout">Vyity</button>
      </header>

      <nav className="admin-nav">
        <button 
       className={`tab ${activeTab === 'portfolio' ? 'active' : ''}`}
        onClick={() => setActiveTab('portfolio')}
        >
          Portfolio ({portfolio.length})
    </button>
        <button 
          className={`tab ${activeTab === 'reviews' ? 'active' : ''}`}
          onClick={() => setActiveTab('reviews')}
        >
   Vidhuky ({reviews.length})
  </button>
        <button 
          className={`tab ${activeTab === 'contacts' ? 'active' : ''}`}
          onClick={() => setActiveTab('contacts')}
    >
       Zaiavky ({contacts.filter(c => !c.isProcessed).length})
        </button>
      </nav>

      <main className="admin-content">
     {loading ? (
          <div className="loading">Zavantazhennia...</div>
 ) : (
          <>
            {activeTab === 'portfolio' && (
              <PortfolioAdmin 
                items={portfolio} 
     onDelete={(id) => handleDelete('portfolio', id)}
 onEdit={setEditItem}
     onAdd={() => { setEditItem(null); setShowForm(true); }}
            showForm={showForm}
   setShowForm={setShowForm}
       editItem={editItem}
        onSave={loadData}
       />
        )}
   {activeTab === 'reviews' && (
              <ReviewsAdmin 
         items={reviews} 
     onDelete={(id) => handleDelete('reviews', id)}
                onEdit={setEditItem}
     onAdd={() => { setEditItem(null); setShowForm(true); }}
           showForm={showForm}
           setShowForm={setShowForm}
      editItem={editItem}
   onSave={loadData}
           />
 )}
            {activeTab === 'contacts' && (
              <ContactsAdmin 
        items={contacts} 
    onDelete={(id) => handleDelete('contacts', id)}
            onMarkProcessed={handleMarkProcessed}
              />
            )}
          </>
        )}
      </main>
    </div>
  )
}

function PortfolioAdmin({ items, onDelete, onAdd, showForm, setShowForm, editItem, onSave, onEdit }) {
  const [form, setForm] = useState({ title: '', category: categories[0], description: '', isVisible: true, sortOrder: 0 })
  const [image, setImage] = useState(null)

  useEffect(() => {
    if (editItem) {
      setForm({
        title: editItem.title,
        category: editItem.category,
        description: editItem.description || '',
        isVisible: editItem.isVisible,
        sortOrder: editItem.sortOrder
      })
setShowForm(true)
    }
  }, [editItem])

  const handleSubmit = async (e) => {
    e.preventDefault()
  const formData = new FormData()
    formData.append('title', form.title)
    formData.append('category', form.category)
    formData.append('description', form.description)
    formData.append('isVisible', form.isVisible)
    formData.append('sortOrder', form.sortOrder)
    if (image) formData.append('image', image)

    try {
      if (editItem) {
        await api.updatePortfolioItem(editItem.id, formData)
      } else {
        await api.createPortfolioItem(formData)
      }
      setShowForm(false)
setForm({ title: '', category: categories[0], description: '', isVisible: true, sortOrder: 0 })
      setImage(null)
      onSave()
    } catch (err) {
      console.error('Error saving:', err)
    }
  }

  return (
    <div>
      <div className="admin-toolbar">
     <h2>Portfolio</h2>
        <button className="btn-add" onClick={onAdd}>+ Dodaty</button>
      </div>

      {showForm && (
        <form className="admin-form" onSubmit={handleSubmit}>
          <input
    type="text"
 placeholder="Nazva"
  value={form.title}
   onChange={(e) => setForm({...form, title: e.target.value})}
  required
          />
       <select 
     value={form.category} 
  onChange={(e) => setForm({...form, category: e.target.value})}
   >
    {categories.map(cat => (
    <option key={cat} value={cat}>{cat}</option>
        ))}
          </select>
          <textarea
      placeholder="Opys"
         value={form.description}
  onChange={(e) => setForm({...form, description: e.target.value})}
          />
      <input
    type="file"
         accept="image/*"
            onChange={(e) => setImage(e.target.files[0])}
          />
          <input
            type="number"
          placeholder="Poriadok"
            value={form.sortOrder}
         onChange={(e) => setForm({...form, sortOrder: parseInt(e.target.value)})}
          />
     <label>
     <input
           type="checkbox"
              checked={form.isVisible}
          onChange={(e) => setForm({...form, isVisible: e.target.checked})}
    />
  Vidymo
          </label>
       <div className="form-actions">
            <button type="submit" className="btn-save">Zberehty</button>
       <button type="button" className="btn-cancel" onClick={() => setShowForm(false)}>Skasuvatyi</button>
          </div>
        </form>
      )}

      <div className="admin-grid">
        {items.map(item => (
          <div key={item.id} className={`admin-card ${!item.isVisible ? 'hidden' : ''}`}>
  <img src={item.imageUrl || 'https://picsum.photos/200/150?random=' + item.id} alt={item.title} />
        <div className="card-content">
      <h3>{item.title}</h3>
      <span className="category">{item.category}</span>
            {!item.isVisible && <span className="badge">Pryhovano</span>}
    </div>
      <div className="card-actions">
      <button onClick={() => onEdit(item)}>Edit</button>
      <button onClick={() => onDelete(item.id)} className="delete">Delete</button>
         </div>
       </div>
 ))}
      </div>
    </div>
  )
}

function ReviewsAdmin({ items, onDelete, onAdd, showForm, setShowForm, editItem, onSave, onEdit }) {
  const [form, setForm] = useState({ name: '', text: '', rating: 5, isVisible: true })

  useEffect(() => {
    if (editItem) {
      setForm({
        name: editItem.name,
      text: editItem.text,
        rating: editItem.rating,
   isVisible: editItem.isVisible
      })
      setShowForm(true)
 }
  }, [editItem])

  const handleSubmit = async (e) => {
    e.preventDefault()
    try {
      if (editItem) {
        await api.updateReview(editItem.id, form)
      } else {
        await api.createReview(form)
      }
      setShowForm(false)
      setForm({ name: '', text: '', rating: 5, isVisible: true })
onSave()
    } catch (err) {
      console.error('Error saving:', err)
    }
  }

  return (
    <div>
  <div className="admin-toolbar">
  <h2>Vidhuky</h2>
        <button className="btn-add" onClick={onAdd}>+ Dodaty</button>
      </div>

{showForm && (
        <form className="admin-form" onSubmit={handleSubmit}>
          <input
        type="text"
   placeholder="Imia"
            value={form.name}
            onChange={(e) => setForm({...form, name: e.target.value})}
            required
          />
       <textarea
    placeholder="Tekst vidhuku"
  value={form.text}
     onChange={(e) => setForm({...form, text: e.target.value})}
      required
        />
          <select 
       value={form.rating} 
    onChange={(e) => setForm({...form, rating: parseInt(e.target.value)})}
>
          {[5,4,3,2,1].map(r => (
        <option key={r} value={r}>{r} {'?'.repeat(r)}</option>
            ))}
          </select>
       <label>
      <input
       type="checkbox"
              checked={form.isVisible}
 onChange={(e) => setForm({...form, isVisible: e.target.checked})}
      />
            Vidymo
          </label>
      <div className="form-actions">
      <button type="submit" className="btn-save">Zberehty</button>
            <button type="button" className="btn-cancel" onClick={() => setShowForm(false)}>Skasuvatyi</button>
        </div>
        </form>
      )}

      <div className="admin-list">
        {items.map(item => (
     <div key={item.id} className={`admin-list-item ${!item.isVisible ? 'hidden' : ''}`}>
     <div className="list-content">
 <strong>{item.name}</strong>
      <span className="rating">{'?'.repeat(item.rating)}</span>
            <p>{item.text}</p>
              {!item.isVisible && <span className="badge">Pryhovano</span>}
      </div>
 <div className="list-actions">
         <button onClick={() => onEdit(item)}>Edit</button>
    <button onClick={() => onDelete(item.id)} className="delete">Delete</button>
            </div>
          </div>
        ))}
      </div>
  </div>
  )
}

function ContactsAdmin({ items, onDelete, onMarkProcessed }) {
  return (
    <div>
      <div className="admin-toolbar">
<h2>Zaiavky</h2>
      </div>

      <div className="admin-list">
{items.map(item => (
          <div key={item.id} className={`admin-list-item ${item.isProcessed ? 'processed' : 'new'}`}>
            <div className="list-content">
   <div className="contact-header">
  <strong>{item.name}</strong>
            <a href={`tel:${item.phone}`} className="phone">{item.phone}</a>
         {!item.isProcessed && <span className="badge new">Nova!</span>}
   {item.isProcessed && <span className="badge processed">Opratsiiovano</span>}
              </div>
       {item.message && <p>{item.message}</p>}
            <small>{new Date(item.createdAt).toLocaleString()}</small>
 </div>
        <div className="list-actions">
     {!item.isProcessed && (
      <button onClick={() => onMarkProcessed(item.id)} className="process">Oprotsiuvaty</button>
        )}
     <button onClick={() => onDelete(item.id)} className="delete">Delete</button>
            </div>
   </div>
        ))}
        {items.length === 0 && <p className="empty">Nemae zaiavok</p>}
      </div>
 </div>
  )
}
