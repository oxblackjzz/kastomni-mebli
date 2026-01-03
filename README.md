# Kastomni Mebli - Custom Furniture Website

Modern furniture website with admin panel.

## Tech Stack
- **Backend**: .NET 9, SQLite, BCrypt
- **Frontend**: React + Vite
- **Styling**: CSS (Cashmere & Tobacco Oak theme)

## Local Development

```bash
cd CustomniMebli.Server
dotnet run
```

Frontend runs via Vite dev server on port 5173.

## Admin Panel
- URL: Click "Admin" in footer
- Login: `admin`
- Password: `admin123`

## API Endpoints
- `POST /api/auth/login` - Login
- `POST /api/auth/reset-admin` - Reset admin password
- `GET /api/portfolio` - Get portfolio items
- `GET /api/reviews` - Get reviews
- `POST /api/contacts` - Submit contact form

## Deploy to Railway
1. Push to GitHub
2. Connect to Railway
3. Deploy automatically

## Environment Variables
- `ASPNETCORE_ENVIRONMENT` - Production/Development
- `ASPNETCORE_URLS` - Server URL (default: http://+:8080)
