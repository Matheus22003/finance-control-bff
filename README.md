# 🔌 Finance Control — BFF

Backend for Frontend responsável por autenticação, segurança e orquestração.

## Responsabilidades
- Login e emissão de JWT
- Validação de tokens
- Orquestração entre microserviços
- Acesso controlado à IA

## Stack
- .NET 10
- Minimal APIs

## 📚 Documentação
➡️ https://github.com/Matheus22003/finance-control-docs

## ▶️ Running (Development)

### API
- Health: `GET /health`

### OpenAPI
- Spec (JSON): `GET /openapi/v1.json`

### API Docs UI (Development only)
- Swagger UI: `/swagger`
- Scalar UI: `/scalar`

> These UIs are enabled only in Development environment.
## 🔐 Authentication
This service uses JWT Bearer authentication.
- Login endpoint issues JWTs (MVP)
- Protected endpoints require `Authorization: Bearer <token>`