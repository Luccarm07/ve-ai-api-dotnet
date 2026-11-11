# Vê AI · API .NET (Minimal APIs)

Global Solution 2026/2 · FIAP 2TDSPX · **Advanced Business Development with .NET** · tema **CitizenTech**

O **Vê AI** é um assistente em português para pessoas com deficiência visual. Enquanto a pessoa anda, o celular fotografa o caminho e a IA narra por áudio **o que está à frente, com os perigos primeiro**. Quando a IA identifica uma barreira de acessibilidade (buraco, calçada obstruída, rampa irregular, falta de piso tátil, semáforo sem som), o app pergunta se a pessoa quer avisar a rede de apoio. O **relato** chega com GPS ao **gestor da região** (ONG/CRAS), que faz a triagem e o encaminha ao órgão público até a resolução, e quem relatou ou confirmou é avisado a cada passo.

Esta é a API consumida pelo app mobile. Ela implementa **o mesmo contrato** da [API Java](https://github.com/lgaxd/ve-ai-api-java), sobre **o mesmo banco Oracle** do repositório [database](https://github.com/lgaxd/ve-ai-database): o app funciona apontando para qualquer uma das duas.

| Integrante | RM |
|---|---:|
| Lucas Grillo Alcântara | 561413 |
| Pietro Ferreira Gomes Abrahamian | 561469 |
| Pedro Peres Benitez | 561792 |
| Lucca Ramos Mussumecci | 562027 |

---

## 1. Requisitos da disciplina: onde está cada um

| # | Critério (pontos) | Como foi feito | Onde está |
|---|---|---|---|
| 01 | **Minimal API RESTful, validações, códigos HTTP e OpenAPI/Swagger** (20) | 18 rotas em Minimal APIs (`MapGroup`), validação por DataAnnotations num filtro de endpoint, erros no formato único `{timestamp, status, erro, mensagem, campos[]}` com 400, 401, 403, 404, 409, 413, 415, 422, 429 e 502. OpenAPI nativo do ASP.NET Core + Swagger UI, com descrição, exemplos e respostas de cada rota | [Endpoints/](src/VeAI.Api/Endpoints), [Erros/](src/VeAI.Api/Erros), [OpenApiConfig.cs](src/VeAI.Api/Configuracao/OpenApiConfig.cs) |
| 02 | **JWT: autenticação e autorização** (20) | JWT HS256 (BCrypt nas senhas), perfis **CIDADAO** e **GESTOR** como políticas; toda rota exige login, salvo login, cadastro, health e Swagger. O gestor só vê e altera relatos **das regiões que atende** (403). Botão **Authorize** no Swagger | [AutenticacaoJwt.cs](src/VeAI.Api/Seguranca/AutenticacaoJwt.cs), [Seguranca.cs](src/VeAI.Infrastructure/Seguranca/Seguranca.cs) |
| 03 | **Idempotência** (15) | `POST /relatos` aceita o header **`Idempotency-Key`**: a mesma chave com o mesmo corpo devolve a mesma resposta (header `Idempotent-Replayed: true`) sem criar outro relato; com outro corpo, 422. A resposta é gravada **na mesma transação** da ação (tabela `IDEMPOTENCIA`), então nem envios simultâneos duplicam. Além disso, a **regra dos 30 m** é idempotente por natureza: repetir o relato vira `JA_REGISTRADO` | [IdempotenciaServico.cs](src/VeAI.Application/Servicos/IdempotenciaServico.cs), [RelatoServico.cs](src/VeAI.Application/Servicos/RelatoServico.cs) |
| 04 | **Rate limiting com 429** (15) | Middleware nativo `Microsoft.AspNetCore.RateLimiting` com 3 políticas: **fotos** (20/min por usuário, janela fixa), **login** (10/min por IP, janela deslizante, contra força bruta) e **global** (300/min, token bucket). Excesso → **429**, header **`Retry-After`** e a mensagem "Muitas fotos em pouco tempo. Aguarde N segundos e tente de novo." | [LimitesRequisicao.cs](src/VeAI.Api/Configuracao/LimitesRequisicao.cs) |
| 05 | **Testes xUnit: unitários e de integração** (20) | **133 testes** (131 rodam em qualquer máquina; 2 usam um Oracle real): 80 unitários (regras de domínio, serviços com dublês, JWT/BCrypt, cliente da OpenAI com HTTP simulado, mensageria) e 53 de integração com `WebApplicationFactory` (todas as rotas, 401/403/404/409/422/429, idempotência, notificações e um **teste de contrato** contra o OpenAPI da API Java) | [tests/VeAI.Tests](tests/VeAI.Tests) |
| 06 | **Health Checks: API, banco e serviços externos** (10) | `/health` (tudo), `/health/live` (processo) e `/health/ready` (dependências), em JSON: **api**, **oracle** (consulta real), **rabbitmq** (conexão + mensagens na fila e na DLQ) e **ia** (OpenAI, sem gastar tokens). Banco fora → 503 *Unhealthy*; RabbitMQ ou IA fora → *Degraded* (a API segue atendendo) | [Program.cs](src/VeAI.Api/Program.cs), [IaHealthCheck.cs](src/VeAI.Infrastructure/Ia/IaHealthCheck.cs), [Rabbit.cs](src/VeAI.Infrastructure/Mensageria/Rabbit.cs) |

Tecnologias obrigatórias: **ASP.NET Core 10 Minimal APIs** e **EF Core 10** (provedor Oracle).

---

## 2. Como rodar

### Opção A · Tudo com Docker (só precisa do Docker)

API, Oracle XE e RabbitMQ sobem juntos; o Oracle já vem com o schema e os dados de demonstração.

```bash
git clone https://github.com/Luccarm07/ve-ai-api-dotnet.git
cd ve-ai-api-dotnet
docker compose up -d --build     # 1ª vez: baixa as imagens e carrega o banco (3 a 5 min)
docker compose ps                # aguarde oracle e rabbitmq "healthy" e a api "Up"
```

| O quê | Onde |
|---|---|
| **Swagger** | http://localhost:8081/swagger |
| Saúde | http://localhost:8081/health |
| RabbitMQ (painel) | http://localhost:15672 (`veai` / `veai`) |
| Oracle (SQL Developer) | `localhost:1521`, serviço `XEPDB1`, usuário `veai` / senha `veai` |

Parar: `docker compose down` (os dados ficam no volume; `docker compose down -v` apaga e recarrega o seed na próxima subida).

> A imagem do Oracle XE é `linux/amd64`. Em Mac com Apple Silicon ela roda emulada, mais devagar.
> Portas ocupadas? Defina `API_PORT`, `ORACLE_PORT`, `RABBITMQ_PORT` ou `RABBITMQ_PAINEL_PORT` num arquivo `.env`.

### Opção B · Desenvolvimento (.NET SDK 10)

```bash
docker compose up -d oracle rabbitmq    # só a infraestrutura
cp .env.example .env                    # opcional: JWT, RabbitMQ, IA, limites (o .env nunca vai para o git)
dotnet run --project src/VeAI.Api       # Swagger: http://localhost:5046/swagger
```

Sem `.env`, em desenvolvimento a API usa o Oracle do Docker (`appsettings.Development.json`) e a fila em memória.

### Opção C · Oracle da FIAP

No `.env`, use a linha `ConnectionStrings__Oracle` da opção 2 com o seu RM e senha. O schema precisa ter os scripts de [database/oracle](database/oracle) (ou o `00_instalar.sql` do repositório database). Se a API Java já rodou nesse schema, nada precisa ser feito.

### Usuários de teste (senha de todos: `Veai@2026`)

| E-mail | Perfil |
|---|---|
| `ana.souza@veai.dev`, `bruno.lima@veai.dev`, `carla.mendes@veai.dev`, `diego.rocha@veai.dev` | CIDADAO |
| `marina.alves@ong-veai.dev` (Pinheiros e Sé), `rafael.costa@cras-veai.dev` (Vila Mariana) | GESTOR |

No Swagger: `POST /auth/login` → copie o `token` → **Authorize**.

### Configuração (`.env`)

Todas as opções estão em [.env.example](.env.example). Nenhum segredo fica no código ou no repositório:

| Variável | Para quê | Sem ela |
|---|---|---|
| `ConnectionStrings__Oracle` | banco | em desenvolvimento, o Oracle do Docker |
| `Jwt__Segredo` | chave do JWT (32+ caracteres) | chave temporária gerada a cada inicialização |
| `Rabbit__Url` | RabbitMQ | eventos numa fila em memória, com a mesma lógica |
| `Ia__Provedor`, `OPENAI_API_KEY` | IA real (`openai`) | descritor **falso**: respostas fixas, sem custo |
| `RateLimit__FotosPorMinuto` e afins | limites | 20 fotos, 10 logins e 300 requisições por minuto |

---

## 3. Testes

```bash
dotnet test
```

Os testes não precisam de Docker nem de banco: a integração sobe a API inteira em memória (`WebApplicationFactory`) com **SQLite** no lugar do Oracle e o mesmo modelo do EF Core. Dois testes extras rodam contra um **Oracle de verdade** quando a variável `VEAI_ORACLE_TESTE` tem a connection string (sem ela aparecem como *Skipped*):

```bash
docker compose up -d oracle
VEAI_ORACLE_TESTE="User Id=veai;Password=veai;Data Source=localhost:1521/XEPDB1" dotnet test
```

| Grupo | O que cobre |
|---|---|
| `Unit/Domain` | prioridade, máquina de estados, regra dos 30 m, exclusão (409), sugestão de relato, assinatura da foto |
| `Unit/Application` | autenticação, idempotência (repetir, conflito, vencimento, falha), contexto do modo automático, memória de sugestões |
| `Unit/Infrastructure` | **mapeamento EF Core × DDL Oracle** (toda coluna mapeada existe no script e vice-versa), BCrypt compatível com o seed, JWT, cliente da OpenAI, mensagens da fila, 3 tentativas + DLQ |
| `Integration` | todas as rotas, perfis, 401/403/404/409/413/415/422/429, idempotência, notificações ponta a ponta e o **contrato com a API Java** |

---

## 4. Endpoints

| Grupo | Rotas | Perfil |
|---|---|---|
| Autenticação | `POST /auth/registrar` · `POST /auth/login` | público (10/min por IP) |
| Usuário | `GET /usuarios/me` · `PUT /usuarios/me/preferencias` | logado |
| Regiões | `GET /regioes` | logado |
| Descrições | `POST /descricoes` (multipart) · `GET /descricoes` · `GET`/`DELETE /descricoes/{id}` · `PATCH /descricoes/{id}/avaliacao` · `POST /descricoes/{id}/pergunta` | CIDADAO (fotos: 20/min) |
| Relatos | `POST /relatos` (Idempotency-Key) · `GET /relatos/meus` · `GET`/`PUT`/`DELETE /relatos/{id}` | CIDADAO |
| Gestor | `GET /gestor/relatos` · `GET /gestor/relatos/{id}` · `GET /gestor/relatos/{id}/historico` · `PATCH /gestor/relatos/{id}/status` | GESTOR (só as regiões dele) |
| Notificações | `GET /notificacoes` · `PATCH /notificacoes/{id}/lida` | logado |
| Saúde | `GET /health` · `/health/live` · `/health/ready` | público |

### Regras de negócio (as mesmas do banco e da API Java)

- **Regra dos 30 m:** relato novo do mesmo tipo, a até 30 m de um não finalizado, vira **+1 apoio**: `201 CRIADO`, `200 APOIO_REGISTRADO` ou `200 JA_REGISTRADO` (autor ou quem já apoiou). Região escolhida pelo GPS (centro mais próximo).
- **Prioridade:** peso da gravidade (BAIXA 1, MEDIA 3, ALTA 5) × (1 + apoios) + dias em aberto; 0 quando finalizado. Recalculada de hora em hora.
- **Máquina de estados:** `ABERTO → EM_TRIAGEM → ENCAMINHADO → RESOLVIDO`, ou `DESCARTADO` a partir de qualquer status não final; não pula etapa nem volta (422). ENCAMINHADO exige protocolo; DESCARTADO, motivo.
- **Exclusão:** só enquanto ABERTO e **sem apoios** (com apoios → **409**).
- **Descrição por IA:** perigos primeiro, com posição e distância; a foto **nunca é gravada**. Modo automático (`anteriorId`): com novidade, fala só o que mudou; sem novidade, confirma numa frase curta. Sugestão de relato quando a confiança da barreira é ≥ 0,7, sem repetir a mesma em 2 minutos.

### Mensageria (RabbitMQ)

```
API ──(depois do commit)──▶ exchange veai.relatos (topic)
                              │ relato.criado · relato.apoiado · relato.status
                              ▼
                        fila veai.notificacoes ──▶ consumidor (ack manual, 3 tentativas)
                              │ falhou de vez                       │
                              ▼                                     ▼
                  veai.relatos.dlx ──▶ veai.notificacoes.dlq    tabela NOTIFICACAO
```

| Evento | Quem é notificado | Exemplo (lido em voz alta pelo app) |
|---|---|---|
| `relato.criado` | gestores da região | "Novo relato de calçada danificada em Pinheiros." |
| `relato.apoiado` | autor | "Mais uma pessoa confirmou seu relato de calçada danificada. Agora 3 pessoas já confirmaram." |
| `relato.status` | autor e apoiadores | "Seu relato de problema na rampa foi encaminhado ao órgão responsável. Protocolo SP156-2026-0099999." |

---

## 5. Arquitetura

Clean Architecture em 4 projetos + testes. As dependências apontam para dentro: o **domínio** não conhece HTTP, banco nem RabbitMQ, por isso as regras são testadas isoladas.

```
src/
  VeAI.Domain/          entidades, enums, regras puras (prioridade, estados, 30 m), eventos e exceções de negócio
  VeAI.Application/     DTOs (contrato JSON), portas (repositórios, IA, mensageria) e serviços de aplicação
  VeAI.Infrastructure/  EF Core (Oracle), JWT/BCrypt, OpenAI e descritor falso, RabbitMQ, health checks, tarefas periódicas
  VeAI.Api/             Program.cs, Minimal APIs, validação, erros, JWT, rate limiting, OpenAPI/Swagger
tests/VeAI.Tests/       xUnit: Unit/ e Integration/
database/oracle/        scripts do schema (os mesmos da API Java e do repositório database)
docker/                 carga do schema no Oracle do Docker
docs/                   contrato OpenAPI da API Java (usado pelo teste de contrato)
```

Decisões que valem destacar:

- **EF Core sobre o schema existente:** a API não cria tabelas; mapeia as do repositório database. Um teste confere o mapeamento contra o DDL a cada `dotnet test`.
- **Gatilhos do Oracle respeitados:** o `trg_conta_apoio` soma o apoio no banco; a API grava na ordem certa para nunca contar duas vezes.
- **Concorrência:** a busca da regra dos 30 m e as mudanças de status usam `SELECT ... FOR UPDATE` no Oracle. Testado com 3 apoios simultâneos (resultado: 3) e 4 envios simultâneos com a mesma `Idempotency-Key` (resultado: 1 relato).
- **Eventos depois do commit:** nada é avisado se a operação for desfeita; com o RabbitMQ fora, a operação do usuário continua funcionando.
- **Mensagens pensadas para voz:** erros e notificações são frases curtas em português, porque o app as lê em voz alta.

---

## 6. Segurança e privacidade

- Senhas com **BCrypt**; nenhum segredo no código: banco, JWT e OpenAI vêm do `.env` (fora do git). As senhas `veai/veai` do `docker-compose.yml` são de containers locais e descartáveis.
- A foto é processada só em memória e **nunca é gravada**; o GPS existe apenas no relato.
- Login e cadastro limitados por IP; o login não revela se o e-mail existe (mesma mensagem e mesmo tempo de resposta).
