# Finance Control - BFF

Backend for Frontend responsável pela autenticação, segurança e futura agregação dos serviços Finance e Debt. O frontend Angular acessa somente este serviço.

## Stack

- .NET 10
- Minimal APIs
- JWT Bearer
- OpenAPI nativo 3.1
- Scalar e Swagger UI
- ProblemDetails (RFC 7807)

## Endpoints

| Método | Caminho | Acesso | Descrição |
|---|---|---|---|
| `GET` | `/health` | Público | Estado da aplicação |
| `POST` | `/api/v1/auth/login` | Público | Emite um JWT para o usuário de demonstração |
| `GET` | `/api/v1/dashboard` | JWT | Retorna o dashboard mockado |

Em `Development`, a especificação `/openapi/v1.json`, o Scalar em `/scalar/v1` e o Swagger UI em `/swagger` também exigem JWT. Isso mantém `/health` e `/api/v1/auth/login` como os únicos endpoints públicos. Para abrir as UIs no navegador, use uma extensão ou proxy local capaz de enviar o header `Authorization` já na navegação inicial.

## Execução local

Requer o SDK fixado em `10.0.301` pelo `global.json`.

```powershell
dotnet restore FinanceControl.Bff.sln
dotnet run --project src/FinanceControl.Bff/FinanceControl.Bff.csproj
```

Credenciais mock de desenvolvimento:

```text
email: demo@financecontrol.local
password: ChangeMe123!
```

Essas credenciais e a chave JWT existem somente em `appsettings.Development.json`. Para outros ambientes, informe `Jwt__Key`, `DemoUser__Email` e `DemoUser__Password` por variáveis de ambiente.

## Testes

```powershell
dotnet test FinanceControl.Bff.sln --configuration Release
```

## Docker

O Dockerfile usa imagens com versões fixas. O daemon Docker precisa estar instalado para executar estes comandos.

```powershell
docker build --tag finance-control-bff:local .
docker run --rm --publish 8080:8080 `
  --env Jwt__Key="replace-with-a-secret-key-containing-at-least-32-bytes" `
  --env DemoUser__Email="demo@example.com" `
  --env DemoUser__Password="replace-this-password" `
  finance-control-bff:local
```

O container escuta em `http://localhost:8080`. A terminação TLS deve ser feita pelo proxy de entrada, que encaminha `X-Forwarded-Proto`.

## Pacotes com versão direta

Aplicação:

- `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.10`
- `Microsoft.AspNetCore.OpenApi` `10.0.10`
- `Microsoft.OpenApi` `2.11.0`
- `Scalar.AspNetCore` `2.16.17`
- `Swashbuckle.AspNetCore.SwaggerUI` `10.2.3`

Testes:

- `Microsoft.AspNetCore.Mvc.Testing` `10.0.10`
- `Microsoft.NET.Test.Sdk` `18.8.1`
- `xunit` `2.9.3`
- `xunit.runner.visualstudio` `3.1.5`
