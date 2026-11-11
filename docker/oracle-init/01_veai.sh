#!/bin/bash
# =============================================================================
# Vê AI · carga do schema no Oracle XE do Docker (roda só na 1ª inicialização do volume)
#
# A imagem gvenzl executa os *.sql desta pasta como SYS, o que criaria as tabelas no
# schema errado. Por isso este script roda os scripts de database/oracle conectado como
# o usuário da aplicação (APP_USER), em ordem:
#   01_schema.sql         tabelas, constraints e índices (iguais ao repositório database)
#   02_plsql.sql          funções, procedures, gatilhos e pacotes
#   03_extensoes_api.sql  centro das regiões + tabela IDEMPOTENCIA
#   04_seed.sql           dados de demonstração (senha Veai@2026)
# =============================================================================
set -e
export NLS_LANG=AMERICAN_AMERICA.AL32UTF8
CONEXAO="${APP_USER}/${APP_USER_PASSWORD}@//localhost:1521/XEPDB1"

for script in 01_schema 02_plsql 03_extensoes_api 04_seed; do
  echo "VEAI: executando ${script}.sql como ${APP_USER}"
  sqlplus -s "${CONEXAO}" <<SQL
WHENEVER SQLERROR EXIT FAILURE ROLLBACK
SET DEFINE OFF
@/veai-sql/${script}.sql
COMMIT;
EXIT
SQL
done

echo "VEAI: schema carregado. Objetos inválidos (a lista deve sair vazia):"
sqlplus -s "${CONEXAO}" <<SQL
SET HEADING OFF FEEDBACK OFF PAGESIZE 0
SELECT object_type || ' ' || object_name FROM user_objects WHERE status = 'INVALID';
SELECT 'VEAI: usuarios=' || COUNT(*) FROM usuario;
EXIT
SQL
