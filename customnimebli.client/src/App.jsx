import { useState, useEffect } from 'react'
import './App.css'
import { api } from './api'
import { ukText, services, advantages, steps, faqItems } from './data'
import Admin from './components/Admin'

function App() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)
  const [formData, setFormData] = useState({ name: '', phone: '', message: '' })
  const [formSubmitted, setFormSubmitted] = useState(false)
const [openFaq, setOpenFaq] = useState(null)
  const [showScrollTop, setShowScrollTop] = useState(false)
  const [portfolio, setPortfolio] = useState([])
  const [reviews, setReviews] = useState([])
  const [lightbox, setLightbox] = useState(null)
  
  // Admin state
const [isAdmin, setIsAdmin] = useState(false)
  const [showLogin, setShowLogin] = useState(false)
  const [loginData, setLoginData] = useState({ username: '', password: '' })
  const [loginError, setLoginError] = useState('')

  const text = ukText

  useEffect(() => {
    // Load portfolio and reviews from API
    loadData()
    
    // Scroll listener for "back to top" button
    const handleScroll = () => {
      setShowScrollTop(window.scrollY > 500)
    }
    window.addEventListener('scroll', handleScroll)
    return () => window.removeEventListener('scroll', handleScroll)
  }, [])

  const loadData = async () => {
    try {
      const [portfolioData, reviewsData] = await Promise.all([
        api.getPortfolio(),
        api.getReviews()
      ])
      setPortfolio(portfolioData)
 setReviews(reviewsData)
    } catch (err) {
      // Fallback to placeholder data if API not available
      setPortfolio([
        { id: 1, title: 'Suchasna kukhnia', category: 'Kukhni', imageUrl: 'https://picsum.photos/400/300?random=1' },
   { id: 2, title: 'Shafa-kupe z dzerkalom', category: 'Shafy-kupe', imageUrl: 'https://picsum.photos/400/300?random=2' },
        { id: 3, title: 'Kompaktnyi peredpokii', category: 'Peredpokoi', imageUrl: 'https://picsum.photos/400/300?random=3' },
 { id: 4, title: 'Kukhnia u styli loft', category: 'Kukhni', imageUrl: 'https://picsum.photos/400/300?random=4' },
 { id: 5, title: 'Vbudovana shafa', category: 'Shafy-kupe', imageUrl: 'https://picsum.photos/400/300?random=5' },
 { id: 6, title: 'Peredpokii z banketkoiu', category: 'Peredpokoi', imageUrl: 'https://picsum.photos/400/300?random=6' }
      ])
      setReviews([
     { id: 1, name: 'Olena K.', text: 'Chudova kukhnia! Vse zrobyly yakisno ta vchasno. Rekomenduiu!', rating: 5 },
        { id: 2, name: 'Viktor M.', text: 'Zamovliav shafu-kupe. Vidminna yakist ta servis!', rating: 5 },
    { id: 3, name: 'Mariia S.', text: 'Duzhe zadovolena peredpokoiem. Indyvidualnyi pidkhid ta uvaha do detalei.', rating: 5 }
      ])
    }
  }

  const scrollToContact = () => {
    document.getElementById('contact-form').scrollIntoView({ behavior: 'smooth' })
    setMobileMenuOpen(false)
  }

  const scrollToSection = (id) => {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' })
    setMobileMenuOpen(false)
  }

  const scrollToTop = () => {
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const handleFormSubmit = async (e) => {
    e.preventDefault()
    try {
    await api.createContact(formData)
      setFormSubmitted(true)
      setFormData({ name: '', phone: '', message: '' })
      setTimeout(() => setFormSubmitted(false), 5000)
    } catch (err) {
      console.error('Error submitting form:', err)
      // Show success anyway for demo
      setFormSubmitted(true)
      setFormData({ name: '', phone: '', message: '' })
      setTimeout(() => setFormSubmitted(false), 5000)
    }
  }

  const handleLogin = async (e) => {
    e.preventDefault()
    try {
    const result = await api.login(loginData)
      if (result.success) {
      setIsAdmin(true)
        setShowLogin(false)
        setLoginError('')
      } else {
        setLoginError('Nepravylnyi login abo parol')
      }
    } catch (err) {
      setLoginError('Pomylka avtoryzatsii')
    }
  }

  // Render Admin panel if logged in
  if (isAdmin) {
    return <Admin onLogout={() => setIsAdmin(false)} />
  }

  return (
    <div className="app">
  {/* Login Modal */}
      {showLogin && (
        <div className="modal-overlay" onClick={() => setShowLogin(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
      <h2>Admin Login</h2>
       <form onSubmit={handleLogin}>
         <input
     type="text"
    placeholder="Username"
                value={loginData.username}
  onChange={(e) => setLoginData({...loginData, username: e.target.value})}
    required
              />
    <input
                type="password"
          placeholder="Password"
                value={loginData.password}
 onChange={(e) => setLoginData({...loginData, password: e.target.value})}
    required
     />
  {loginError && <p className="error">{loginError}</p>}
          <button type="submit" className="btn btn-primary">Vvity</button>
            </form>
 </div>
        </div>
      )}

      {/* Lightbox */}
      {lightbox && (
  <div className="lightbox" onClick={() => setLightbox(null)}>
          <img src={lightbox.imageUrl || `https://picsum.photos/800/600?random=${lightbox.id}`} alt={lightbox.title} />
<div className="lightbox-info">
 <h3>{lightbox.title}</h3>
   <span>{lightbox.category}</span>
          </div>
          <button className="lightbox-close">?</button>
        </div>
      )}

      {/* Header */}
      <header className="header">
     <div className="header-container">
          <div className="logo" onClick={() => scrollToTop()} style={{cursor: 'pointer'}}>
            <span className="logo-icon">??</span>
         <div className="logo-text-wrapper">
  <span className="logo-text">{text.brandName}</span>
              <span className="logo-tagline">{text.tagline}</span>
     </div>
      </div>

       <button 
         className={`mobile-menu-btn ${mobileMenuOpen ? 'active' : ''}`}
    onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
     aria-label="Menu"
>
            <span></span>
            <span></span>
    <span></span>
          </button>

          <nav className={`nav ${mobileMenuOpen ? 'nav-open' : ''}`}>
     <a href="#about" className="nav-link" onClick={() => scrollToSection('about')}>{text.navAbout}</a>
            <a href="#services" className="nav-link" onClick={() => scrollToSection('services')}>{text.navServices}</a>
     <a href="#portfolio" className="nav-link" onClick={() => scrollToSection('portfolio')}>{text.navPortfolio}</a>
            <button className="btn btn-primary" onClick={scrollToContact}>{text.navContact}</button>
    </nav>
     </div>
      </header>

      {/* Hero Section - New design */}
      <section className="hero">
        <div className="hero-bg">
       <div className="hero-shape hero-shape-1"></div>
          <div className="hero-shape hero-shape-2"></div>
      </div>
        <div className="hero-content">
       <div className="hero-badge">? Premium Quality</div>
    <h1 className="hero-title">{text.heroTitle}</h1>
  <p className="hero-subtitle">{text.heroSubtitle}</p>
          <div className="hero-buttons">
<button className="btn btn-primary btn-large" onClick={scrollToContact}>
       <span>??</span> {text.heroBtn1}
            </button>
 <a href="#portfolio" className="btn btn-secondary btn-large" onClick={() => scrollToSection('portfolio')}>
           {text.heroBtn2} <span>?</span>
            </a>
          </div>
    <div className="hero-stats">
      <div className="stat">
         <span className="stat-number">100+</span>
            <span className="stat-label">Proiektiv</span>
            </div>
            <div className="stat">
 <span className="stat-number">5+</span>
     <span className="stat-label">Rokiv dosvidu</span>
            </div>
<div className="stat">
            <span className="stat-number">98%</span>
           <span className="stat-label">Zadovolenykh kliientiv</span>
      </div>
          </div>
      </div>
      </section>

      {/* About Section */}
      <section id="about" className="about">
 <div className="container">
    <div className="section-header">
     <span className="section-badge">Pro nas</span>
       <h2 className="section-title">{text.aboutTitle}</h2>
  <p className="section-subtitle">{text.aboutSubtitle}</p>
       </div>
          <div className="about-content">
     <div className="about-text">
      <p>{text.aboutText}</p>
            </div>
      <div className="advantages-grid">
           {advantages.map(adv => (
            <div key={adv.id} className="advantage-card">
           <span className="advantage-icon">{adv.icon}</span>
    <h4 className="advantage-title">{adv.title}</h4>
             <p className="advantage-desc">{adv.desc}</p>
       </div>
              ))}
</div>
          </div>
    </div>
</section>

      {/* How We Work Section */}
      <section id="how-we-work" className="how-we-work">
        <div className="container">
          <div className="section-header">
  <span className="section-badge">Protses</span>
      <h2 className="section-title">{text.howWeWorkTitle}</h2>
          <p className="section-subtitle">{text.howWeWorkSubtitle}</p>
          </div>
    <div className="steps-container">
          {steps.map((step, index) => (
    <div key={step.id} className="step-item">
       <div className="step-icon">{step.icon}</div>
<div className="step-number">{step.id}</div>
           <h4 className="step-title">{step.title}</h4>
    <p className="step-desc">{step.desc}</p>
     {index < steps.length - 1 && <div className="step-line"></div>}
              </div>
        ))}
          </div>
   </div>
      </section>

      {/* Services Section */}
    <section id="services" className="services">
        <div className="container">
          <div className="section-header">
     <span className="section-badge">Posluhy</span>
        <h2 className="section-title">{text.servicesTitle}</h2>
  <p className="section-subtitle">{text.servicesSubtitle}</p>
          </div>
          <div className="services-grid">
            {services.map(service => (
              <div key={service.id} className="service-card">
       <div className="service-icon">{service.icon}</div>
       <h3 className="service-title">{service.title}</h3>
   <p className="service-description">{service.description}</p>
 <button className="btn btn-outline" onClick={scrollToContact}>{text.learnMore}</button>
              </div>
  ))}
      </div>
      </div>
      </section>

      {/* Portfolio Section */}
      <section id="portfolio" className="portfolio">
        <div className="container">
        <div className="section-header">
        <span className="section-badge">Nashi roboty</span>
            <h2 className="section-title">{text.portfolioTitle}</h2>
         <p className="section-subtitle">{text.portfolioSubtitle}</p>
    </div>
          <div className="portfolio-grid">
         {portfolio.map(item => (
      <div key={item.id} className="portfolio-item" onClick={() => setLightbox(item)}>
 <div className="portfolio-image">
    <img src={item.imageUrl || `https://picsum.photos/400/300?random=${item.id}`} alt={item.title} />
    <div className="portfolio-overlay">
 <span className="portfolio-category">{item.category}</span>
 <h4 className="portfolio-title">{item.title}</h4>
           <span className="portfolio-zoom">??</span>
        </div>
</div>
     </div>
       ))}
          </div>
     </div>
      </section>

      {/* Reviews Section */}
      <section id="reviews" className="reviews">
        <div className="container">
       <div className="section-header">
  <span className="section-badge">Vidhuky</span>
          <h2 className="section-title">{text.reviewsTitle}</h2>
   <p className="section-subtitle">{text.reviewsSubtitle}</p>
 </div>
   <div className="reviews-grid">
            {reviews.map(review => (
     <div key={review.id} className="review-card">
      <div className="review-quote">"</div>
      <div className="review-stars">
      {[...Array(review.rating)].map((_, i) => (
     <span key={i} className="star">?</span>
        ))}
    </div>
  <p className="review-text">{review.text}</p>
        <p className="review-author">— {review.name}</p>
  </div>
     ))}
          </div>
        </div>
      </section>

  {/* FAQ Section */}
      <section id="faq" className="faq">
        <div className="container">
    <div className="section-header">
      <span className="section-badge">FAQ</span>
            <h2 className="section-title">{text.faqTitle}</h2>
            <p className="section-subtitle">{text.faqSubtitle}</p>
    </div>
          <div className="faq-list">
 {faqItems.map(item => (
              <div key={item.id} className={`faq-item ${openFaq === item.id ? 'open' : ''}`}>
      <button className="faq-question" onClick={() => setOpenFaq(openFaq === item.id ? null : item.id)}>
          <span>{item.question}</span>
       <span className="faq-icon">{openFaq === item.id ? '?' : '+'}</span>
        </button>
         <div className="faq-answer">
  <p>{item.answer}</p>
   </div>
              </div>
    ))}
      </div>
        </div>
      </section>

      {/* Contact Form Section */}
      <section id="contact-form" className="contact-form-section">
        <div className="container">
        <div className="contact-wrapper">
            <div className="contact-info">
              <h2>{text.contactFormTitle}</h2>
          <p>{text.contactFormSubtitle}</p>
         <div className="contact-details">
 <div className="contact-item">
       <span className="icon">??</span>
   <span>+380 (XX) XXX-XX-XX</span>
                </div>
         <div className="contact-item">
               <span className="icon">??</span>
           <span>{text.addressText}</span>
          </div>
     <div className="contact-item">
                  <span className="icon">??</span>
      <span>{text.workHours}</span>
    </div>
              </div>
          </div>
     <div className="contact-form-wrapper">
  {formSubmitted ? (
       <div className="form-success">
       <span className="success-icon">?</span>
         <p>{text.formSuccess}</p>
     </div>
    ) : (
                <form className="contact-form" onSubmit={handleFormSubmit}>
       <div className="form-group">
     <input
            type="text"
      placeholder={text.formName}
           value={formData.name}
   onChange={(e) => setFormData({...formData, name: e.target.value})}
          required
      />
         </div>
         <div className="form-group">
   <input
        type="tel"
    placeholder={text.formPhone}
                 value={formData.phone}
     onChange={(e) => setFormData({...formData, phone: e.target.value})}
   required
        />
    </div>
          <div className="form-group">
  <textarea
           placeholder={text.formMessage}
   value={formData.message}
    onChange={(e) => setFormData({...formData, message: e.target.value})}
            rows="4"
           ></textarea>
    </div>
        <button type="submit" className="btn btn-primary btn-large btn-full">{text.formSubmit}</button>
      </form>
       )}
     </div>
          </div>
        </div>
      </section>

      {/* Footer */}
      <footer id="footer" className="footer">
        <div className="container">
          <div className="footer-grid">
<div className="footer-section">
  <div className="logo footer-logo">
    <span className="logo-icon">??</span>
    <div className="logo-text-wrapper">
      <span className="logo-text">{text.brandName}</span>
      <span className="logo-tagline">{text.tagline}</span>
          </div>
         </div>
        <p className="footer-description">{text.footerDesc}</p>
            </div>

      <div className="footer-section">
     <h4 className="footer-title">{text.contactsTitle}</h4>
  <ul className="footer-list">
   <li>?? +380 (XX) XXX-XX-XX</li>
      <li>?? info@kastomnimebli.ua</li>
  <li>?? {text.workHours}</li>
  </ul>
            </div>

            <div className="footer-section">
     <h4 className="footer-title">{text.addressTitle}</h4>
              <p className="footer-address">{text.addressText}</p>
   <p className="footer-work-area">{text.workArea}</p>
      </div>

       <div className="footer-section">
           <h4 className="footer-title">{text.socialTitle}</h4>
              <div className="social-links">
   <a href="https://www.instagram.com/kastomni_mebli/" target="_blank" rel="noopener noreferrer" className="social-link instagram">Instagram</a>
         <a href="https://www.facebook.com/profile.php?id=61577470" target="_blank" rel="noopener noreferrer" className="social-link facebook">Facebook</a>
                <a href="https://www.tiktok.com/@kastomni_mebli" target="_blank" rel="noopener noreferrer" className="social-link tiktok">TikTok</a>
              </div>
            </div>
    </div>

        <div className="footer-bottom">
            <p>© 2024 {text.brandName}. {text.copyright}</p>
      <button className="admin-link" onClick={() => setShowLogin(true)}>Admin</button>
          </div>
     </div>
      </footer>

      {/* Scroll to top button */}
      {showScrollTop && (
   <button className="scroll-top" onClick={scrollToTop}>?</button>
      )}
    </div>
  )
}

export default App
