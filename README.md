# Consultas de Afiliados

Sistema de gestión de consultas médicas para los afiliados de un plan de salud. Permite registrar afiliados, asignar consultas con profesionales de distintas especialidades y hacer seguimiento del estado de cada consulta.

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)

---

## Backend

```bash
cd backend
dotnet run
```

El servidor levanta en **http://localhost:5000**.

La base de datos SQLite (`consultas.db`) se crea automáticamente en la primera ejecución junto con los datos iniciales.

**Swagger:** http://localhost:5000/swagger

---

## Frontend

```bash
cd frontend
npm install
npm run dev
```

El cliente levanta en **http://localhost:5173**.

---

---

## Tests

```bash
dotnet test
```

---

## Estructura del proyecto

```
/
├── ConsultasPlan.sln
├── backend/                     # API REST (.NET 8 · EF Core · SQLite)
├── frontend/                    # SPA (Vue 3 · Vite)
├── tests/ConsultasPlan.Tests/   # xUnit · EF InMemory · FakeTimeProvider
└── docs/                        # documento técnico
```

> **Documento técnico:** [`docs/DOCUMENTO-TECNICO.md`](docs/DOCUMENTO-TECNICO.md)
> — cambios realizados, supuestos tomados y alcance.
