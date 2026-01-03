const API_BASE = '/api';

export const api = {
  // Portfolio
  getPortfolio: () => fetch(`${API_BASE}/portfolio`).then(res => res.json()),
  createPortfolioItem: (formData) => fetch(`${API_BASE}/portfolio`, {
    method: 'POST',
    body: formData
  }).then(res => res.json()),
  updatePortfolioItem: (id, formData) => fetch(`${API_BASE}/portfolio/${id}`, {
    method: 'PUT',
    body: formData
  }),
  deletePortfolioItem: (id) => fetch(`${API_BASE}/portfolio/${id}`, { method: 'DELETE' }),

  // Reviews
  getReviews: () => fetch(`${API_BASE}/reviews`).then(res => res.json()),
  getReviewsAdmin: () => fetch(`${API_BASE}/reviews/admin`).then(res => res.json()),
  createReview: (data) => fetch(`${API_BASE}/reviews`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  }).then(res => res.json()),
  updateReview: (id, data) => fetch(`${API_BASE}/reviews/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  }),
  deleteReview: (id) => fetch(`${API_BASE}/reviews/${id}`, { method: 'DELETE' }),

  // Contacts
  getContacts: () => fetch(`${API_BASE}/contacts`).then(res => res.json()),
  createContact: (data) => fetch(`${API_BASE}/contacts`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data)
  }).then(res => res.json()),
  markContactProcessed: (id) => fetch(`${API_BASE}/contacts/${id}/process`, { method: 'PUT' }),
  deleteContact: (id) => fetch(`${API_BASE}/contacts/${id}`, { method: 'DELETE' }),

  // Auth
  login: async (data) => {
    const res = await fetch(`${API_BASE}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data)
    });
    if (res.ok) {
      return await res.json();
    }
    return { success: false, message: 'Login failed' };
  },
};
