# Changelog

## [Unreleased]

### Added

- envio transacional pelo Brevo usando a API HTTPS;
- persistência das chaves ASP.NET Core Data Protection no PostgreSQL do BFF;
- workflow de publicação multiarch `linux/amd64` e `linux/arm64` no GHCR.

## [0.1.0] - 2026-08-13

### Added

- autenticação com ASP.NET Core Identity, JWT e refresh token rotativo;
- confirmação de e-mail, recuperação de senha e sessões por dispositivo;
- perfil, preferências, avatar, exportação e exclusão segura de conta;
- fachadas tipadas e dashboard agregado para Finance e Debt;
- notificações persistentes e SignalR autenticado;
- integração sanitizada com providers de IA compatíveis com OpenAI;
- respostas determinísticas para perguntas financeiras factuais;
- rate limiting, ProblemDetails e correlação distribuída de requisições;
- OpenAPI, Scalar, Swagger UI e proteção de contrato no CI;
- persistência PostgreSQL e migrations do Entity Framework Core.

[Unreleased]: https://github.com/Matheus22003/finance-control-bff/compare/v0.1.0...develop
[0.1.0]: https://github.com/Matheus22003/finance-control-bff/releases/tag/v0.1.0
