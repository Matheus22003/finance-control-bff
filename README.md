# Finance Control - BFF

Backend for Frontend responsável pela autenticação, segurança, fachadas CRUD e agregação dos serviços Finance e Debt. O frontend Angular acessa somente este serviço.

## Stack

- .NET 10
- Minimal APIs
- JWT Bearer
- Typed `HttpClient`
- SignalR autenticado por JWT
- OpenAPI nativo 3.1
- Scalar e Swagger UI
- ProblemDetails (RFC 7807)

## Endpoints públicos

| Método | Caminho | Descrição |
|---|---|---|
| `GET` | `/health` | Estado da aplicação |
| `POST` | `/api/v1/auth/register` | Cria uma conta e envia a confirmação por e-mail |
| `POST` | `/api/v1/auth/login` | Autentica e cria uma sessão por dispositivo |
| `POST` | `/api/v1/auth/refresh` | Rotaciona o refresh token em cookie HttpOnly |
| `POST` | `/api/v1/auth/confirm-email` | Confirma o e-mail usando um token temporário |
| `POST` | `/api/v1/auth/resend-confirmation` | Reenvia a confirmação sem revelar contas existentes |
| `POST` | `/api/v1/auth/forgot-password` | Envia o link de recuperação com resposta neutra |
| `POST` | `/api/v1/auth/reset-password` | Redefine a senha e revoga todas as sessões |

Todos os demais endpoints exigem JWT.

## Endpoints protegidos

| Grupo | Caminhos |
|---|---|
| Dashboard | `GET /api/v1/dashboard` |
| Finance | resumo, tendência, projeção de caixa, metas com aportes manuais ou vinculados ao saldo disponível de receitas, detalhamento da distribuição de cada receita, categorias padrão e personalizadas, filtros, CRUD de receitas/despesas, recorrências e orçamento mensal em `/api/v1/finance` |
| Pessoas | CRUD em `/api/v1/people` |
| Dívidas | resumo, CRUD, pagamentos, histórico e registro/confirmação do plano simplificado em `/api/v1/debts` |
| Notificações | caixa persistente, sincronização de alertas, leitura e contagem em `/api/v1/notifications`; hub em `/api/v1/notifications/hub` |
| Segurança | troca de senha em `POST /api/v1/auth/change-password` e gestão em `/api/v1/auth/sessions` |

Na área de conta, `/api/v1/users/me` oferece perfil, preferências, avatar, troca de e-mail, exportação e exclusão.

A exclusão exige senha e a confirmação literal `EXCLUIR`. O BFF consulta pendências no Debt Service, remove dados privados nos serviços de domínio e apaga a identidade por último. Dívidas abertas, pagamentos pendentes, planos simplificados ativos e grupos administrados bloqueiam a operação.

O arquivo [FinanceControl.Bff.http](src/FinanceControl.Bff/FinanceControl.Bff.http) contém exemplos completos de todas as operações.

Em `Development`, a especificação `/openapi/v1.json`, o Scalar em `/scalar/v1` e o Swagger UI em `/swagger` também exigem JWT. Além do health, somente os endpoints de entrada, confirmação e recuperação listados acima são públicos; troca de senha, logout e sessões exigem JWT.

## Notificações em tempo real

O BFF persiste notificações direcionadas aos usuários envolvidos em amizades, grupos, dívidas, pagamentos e liquidações simplificadas. Alterações de despesa ou orçamento geram alertas ao cruzar 80% ou 100% do limite mensal da categoria. Metas geram alertas quando são concluídas, ficam a até 30 dias do prazo ou vencem sem atingir o valor esperado. Depois da persistência, o hub SignalR envia `notificationReceived` apenas às conexões autenticadas daquele usuário.

`POST /api/v1/notifications/sync` reavalia orçamento e metas no estado atual. Cada regra usa uma chave persistente de deduplicação, portanto abrir o site, reconectar o SignalR ou colocar um futuro aplicativo em primeiro plano não repete um alerta já emitido para o mesmo evento.

O evento em tempo real é um aviso de mudança; os clientes sempre consultam novamente os endpoints REST protegidos para obter o estado oficial. Essa combinação entre sincronização REST e entrega SignalR pode ser reutilizada por clientes web e mobile. O token do hub é aceito pela query string somente no caminho restrito `/api/v1/notifications/hub`, conforme a limitação dos transportes WebSocket/SSE no navegador.

## Integrações com Finance e Debt

### Análise financeira e de dívidas

`POST /api/v1/ai/analyze` é protegido por JWT e limitado a cinco solicitações por usuário a cada cinco minutos fora do ambiente de testes. O body aceita `month` no formato `yyyy-MM`.

`POST /api/v1/ai/ask` recebe uma pergunta de 3 a 500 caracteres sobre receitas, despesas e dívidas. O chat não é persistido e cada pergunta é respondida de forma independente com o estado atual dos serviços. A rota permite até 15 perguntas por usuário a cada cinco minutos fora dos testes.

O BFF combina o resumo mensal do Finance Service com o contexto analítico calculado pelo Debt Service. Antes de chamar o provedor, remove identificadores, nomes, e-mails e descrições; grupos são enviados apenas como `Grupo 1`, `Grupo 2` ou `Sem grupo`.

A interface `IAiAnalysisProvider` possui duas implementações:

- `Mock`: padrão local, determinístico e sem chave;
- `OpenAiCompatible`: usa `POST /chat/completions` e funciona com Groq, OpenRouter e outros provedores compatíveis.

Nenhum SDK de fornecedor é necessário. Para usar o free tier do Groq:

```text
Ai__Provider=OpenAiCompatible
Ai__BaseUrl=https://api.groq.com/openai/v1/
Ai__ApiKey=sua-chave
Ai__Model=llama-3.1-8b-instant
Ai__TimeoutSeconds=30
Ai__MaxOutputTokens=800
Ai__UseJsonResponseFormat=false
```

Para experimentar o roteador gratuito do OpenRouter, altere somente:

```text
Ai__BaseUrl=https://openrouter.ai/api/v1/
Ai__Model=openrouter/free
Ai__ApplicationUrl=http://localhost:4200
Ai__ApplicationName=Finance Control
```

O provedor externo recebe apenas agregados sanitizados. Nas perguntas detalhadas, pessoas, grupos, dívidas e descrições de lançamentos são convertidos em aliases estáveis para aquela requisição. O BFF restaura os nomes somente depois de receber a resposta, sem expor o mapa de aliases ao Groq. As métricas mostradas no resultado continuam vindo dos cálculos determinísticos dos serviços, nunca do texto gerado pelo modelo. Respostas inválidas retornam ProblemDetails `502`, indisponibilidade ou limite externo retornam `503`, e timeout retorna `504`.

Perguntas factuais sobre quem deve, origem dos valores a pagar, totais de despesas, uso do orçamento e viabilidade das metas são respondidas deterministicamente pelo BFF. O contexto sanitizado da IA inclui limites por categoria, tendência, metas com aliases e projeção dos próximos seis meses. O modelo é usado somente nas perguntas abertas e explicações, evitando inversões entre credor e devedor, tratar projeções como garantias ou inventar valores.

O BFF é a única API consumida pelo frontend. Internamente, ele chama:

- Finance Service em `/api/v1/finance/*`;
- Debt Service em `/api/v1/people/*` e `/api/v1/debts/*`.

O dashboard consulta em paralelo resumo, tendência, orçamento, metas e projeção do Finance Service e o resumo do Debt Service. As demais rotas funcionam como fachadas tipadas e devolvem contratos próprios do BFF.

Em desenvolvimento, o BFF espera o Finance Service em `http://localhost:8081` e o Debt Service em `http://localhost:8082`.

| Variável | Obrigatória | Padrão em Development |
|---|---|---|
| `FinanceService__BaseUrl` | Sim | `http://localhost:8081` |
| `FinanceService__TimeoutSeconds` | Não | `5` |
| `DebtService__BaseUrl` | Sim | `http://localhost:8082` |
| `DebtService__TimeoutSeconds` | Não | `5` |

Falhas de integração são devolvidas pelo BFF como ProblemDetails:

- `400`, `404`, `409` ou `422`: rejeição de negócio preservada do serviço;
- `502`: resposta inválida de um serviço;
- `503`: serviço indisponível;
- `504`: timeout na chamada de um serviço.

## Execução local

Requer o SDK fixado em `10.0.301` pelo `global.json`, o Finance Service na porta `8081` e o Debt Service na porta `8082`.

```powershell
dotnet restore FinanceControl.Bff.sln --locked-mode
dotnet run --project src/FinanceControl.Bff/FinanceControl.Bff.csproj
```

Credenciais mock de desenvolvimento:

```text
email: demo@financecontrol.local
password: ChangeMe123!
```

Para ambientes diferentes de Development, informe `Jwt__Key`, `DemoUser__Email`, `DemoUser__Password`, `FinanceService__BaseUrl` e `DebtService__BaseUrl` por variáveis de ambiente.

## Testes

```powershell
dotnet test FinanceControl.Bff.sln --configuration Release --locked-mode
```

## Integração contínua

O workflow `.github/workflows/ci.yml` é executado em pushes e pull requests para
`main` e `develop`, além de permitir execução manual. A pipeline restaura as
dependências pelo lock file, executa os testes em `Release` e valida a imagem
Docker do BFF.

## Docker

Quando o Finance Service e o Debt Service estiverem publicados no host nas portas `8081` e `8082`:

```powershell
docker build --tag finance-control-bff:local .
docker run --rm --publish 8080:8080 `
  --env Jwt__Key="replace-with-a-secret-key-containing-at-least-32-bytes" `
  --env DemoUser__Email="demo@example.com" `
  --env DemoUser__Password="replace-this-password" `
  --env FinanceService__BaseUrl="http://host.docker.internal:8081" `
  --env DebtService__BaseUrl="http://host.docker.internal:8082" `
  finance-control-bff:local
```

O container escuta em `http://localhost:8080`. A terminação TLS deve ser feita pelo proxy de entrada, que encaminha `X-Forwarded-Proto`.

## Pacotes com versão direta

Aplicação:

- `Microsoft.AspNetCore.Authentication.JwtBearer` `10.0.10`
- `MailKit` `4.17.0`
- `Microsoft.AspNetCore.OpenApi` `10.0.10`
- `Microsoft.OpenApi` `2.11.0`
- `Scalar.AspNetCore` `2.16.17`
- `Swashbuckle.AspNetCore.SwaggerUI` `10.2.3`

Testes:

- `Microsoft.AspNetCore.Mvc.Testing` `10.0.10`
- `Microsoft.NET.Test.Sdk` `18.8.1`
- `xunit` `2.9.3`
- `xunit.runner.visualstudio` `3.1.5`
