# Scripts Oracle do Vê AI

Os mesmos scripts usados pela [API Java](https://github.com/lgaxd/ve-ai-api-java) (migrações Flyway V1–V3 e seed),
que por sua vez são uma cópia fiel do repositório [database](https://github.com/lgaxd/ve-ai-database).
A API .NET **não cria tabelas**: o EF Core apenas mapeia o schema criado por estes scripts. O teste
`MapeamentoOracleTests` confere, a cada `dotnet test`, que toda tabela e coluna mapeada existe aqui.

| Script | Conteúdo |
|---|---|
| `01_schema.sql` | 8 tabelas + `AUDITORIA_RELATO`, constraints e índices. Não apaga nada: só cria o que falta. |
| `02_plsql.sql` | funções, procedures, gatilhos (`trg_auditoria_relato`, `trg_conta_apoio`) e pacotes |
| `03_extensoes_api.sql` | centro das regiões (`REGIAO.latitude/longitude`) e tabela `IDEMPOTENCIA` |
| `04_seed.sql` | dados de demonstração; senha de todos os usuários: `Veai@2026` |

- **Docker:** o `docker-compose.yml` executa os 4 scripts automaticamente na primeira inicialização do Oracle XE.
- **Oracle da FIAP:** rode os 4 scripts em ordem no SQL Developer (F5), com o encoding em UTF-8. Se a API Java já
  rodou nesse schema, tudo já existe e nada precisa ser feito.
