# Title Management Angular UI

This is a standalone Angular frontend for the title module. It talks to the ASP.NET Core REST endpoints under `/api/titles` and keeps the title experience separate from the existing MVC Razor views.

## Run locally

```bash
npm install
npm start
```

Keep the ASP.NET Core backend running on `https://localhost:5001` or update `proxy.conf.json` with the backend URL.

## Main API endpoints

- `GET /api/titles` - list and filter title records.
- `POST /api/titles/imports?save=true|false` - validate/import an Excel file.
- `DELETE /api/titles` - delete selected title ids.
- `GET /api/titles/template` - download the upload template.
- `GET /api/titles/export` - export title records.
