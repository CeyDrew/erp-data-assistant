# Web paneli

Next.js (App Router) + React + TypeScript, Tailwind CSS ve shadcn/ui. Veriler TanStack Query ile
istemci tarafında `NEXT_PUBLIC_API_URL` (varsayılan `http://localhost:5080`) adresindeki API'den okunur.

```bash
npm install
npm run dev          # http://localhost:3000
npm run lint && npm run typecheck && npm run build
npm run screenshots  # API ve web çalışırken docs/screenshots altına görüntü alır (Chrome gerekli)
```
