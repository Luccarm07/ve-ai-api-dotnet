-- =============================================================================
-- Vê AI · Oracle · V2 · Funções, procedures, gatilhos e pacotes
-- Cópia fiel de oracle/02_funcoes.sql, 03_procedures.sql, 04_triggers.sql e
-- 05_pacotes.sql do repositório database, sem os comandos de SQL*Plus
-- (SET / SHOW ERRORS), que o Flyway não executa. Tudo é CREATE OR REPLACE.
--
-- Importante para a API: o trg_conta_apoio incrementa RELATO.total_apoios quando
-- um APOIO_RELATO é inserido. A entidade Relato também incrementa o contador em
-- memória e o Hibernate grava o valor absoluto depois do INSERT do apoio, então o
-- resultado final é o mesmo (n + 1), sem contar duas vezes.
-- =============================================================================

-- ----------------------------------------------------------------------------- 02_funcoes.sql
-- =============================================================================
-- Vê AI · Oracle · 02 · Funções
--   Exigidas pela rubrica (critério 02):
--     fn_prioridade_relato       prioridade de um relato na fila do gestor
--     fn_taxa_descricoes_uteis   % de descrições avaliadas como úteis
--   Auxiliares (usadas por procedures, triggers e pacotes):
--     fn_distancia_metros        distância entre dois pontos GPS (Haversine)
--     fn_calcular_prioridade     fórmula pura da prioridade, sem acesso a tabelas
-- =============================================================================

-- -----------------------------------------------------------------------------
-- fn_distancia_metros: fórmula de Haversine (raio médio da Terra = 6.371 km).
-- Usada na regra de deduplicação: relato do mesmo tipo a até 30 m vira apoio.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_distancia_metros (
  p_lat1 IN NUMBER,
  p_lon1 IN NUMBER,
  p_lat2 IN NUMBER,
  p_lon2 IN NUMBER
) RETURN NUMBER DETERMINISTIC
IS
  c_raio_terra CONSTANT NUMBER := 6371000;
  c_rad        CONSTANT NUMBER := ACOS(-1) / 180;
  v_dlat NUMBER;
  v_dlon NUMBER;
  v_a    NUMBER;
BEGIN
  IF p_lat1 IS NULL OR p_lon1 IS NULL OR p_lat2 IS NULL OR p_lon2 IS NULL THEN
    RETURN NULL;
  END IF;

  v_dlat := (p_lat2 - p_lat1) * c_rad;
  v_dlon := (p_lon2 - p_lon1) * c_rad;
  v_a    := POWER(SIN(v_dlat / 2), 2)
          + COS(p_lat1 * c_rad) * COS(p_lat2 * c_rad) * POWER(SIN(v_dlon / 2), 2);

  RETURN c_raio_terra * 2 * ATAN2(SQRT(v_a), SQRT(1 - v_a));
END fn_distancia_metros;
/

-- -----------------------------------------------------------------------------
-- fn_calcular_prioridade: peso(gravidade) x (1 + apoios) + dias em aberto.
--   BAIXA = 1, MEDIA = 3, ALTA = 5. Relato RESOLVIDO/DESCARTADO = 0.
-- Não consulta tabelas, por isso pode ser chamada dentro de UPDATE RELATO e de
-- triggers sem causar erro de tabela mutante.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_calcular_prioridade (
  p_gravidade    IN VARCHAR2,
  p_total_apoios IN NUMBER,
  p_criado_em    IN DATE,
  p_status       IN VARCHAR2
) RETURN NUMBER
IS
  v_peso NUMBER;
BEGIN
  IF p_status IN ('RESOLVIDO', 'DESCARTADO') THEN
    RETURN 0;
  END IF;

  v_peso := CASE p_gravidade
              WHEN 'BAIXA' THEN 1
              WHEN 'MEDIA' THEN 3
              WHEN 'ALTA'  THEN 5
              ELSE 1
            END;

  RETURN v_peso * (1 + NVL(p_total_apoios, 0))
       + GREATEST(FLOOR(SYSDATE - p_criado_em), 0);
END fn_calcular_prioridade;
/

-- -----------------------------------------------------------------------------
-- fn_prioridade_relato: prioridade atual de um relato pelo id.
-- Erro -20005 se o relato não existir.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_prioridade_relato (
  p_relato_id IN relato.id%TYPE
) RETURN NUMBER
IS
  v_gravidade    relato.gravidade%TYPE;
  v_total_apoios relato.total_apoios%TYPE;
  v_criado_em    relato.criado_em%TYPE;
  v_status       relato.status%TYPE;
BEGIN
  SELECT gravidade, total_apoios, criado_em, status
    INTO v_gravidade, v_total_apoios, v_criado_em, v_status
    FROM relato
   WHERE id = p_relato_id;

  RETURN fn_calcular_prioridade(v_gravidade, v_total_apoios, v_criado_em, v_status);
EXCEPTION
  WHEN NO_DATA_FOUND THEN
    RAISE_APPLICATION_ERROR(-20005, 'Relato ' || p_relato_id || ' não existe.');
END fn_prioridade_relato;
/

-- -----------------------------------------------------------------------------
-- fn_taxa_descricoes_uteis: úteis / avaliadas x 100 (2 casas).
--   p_usuario_id NULL = todos os usuários.
--   Descrições sem avaliação (util NULL) não entram no denominador.
--   Retorna NULL se nenhuma descrição foi avaliada.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION fn_taxa_descricoes_uteis (
  p_usuario_id IN usuario.id%TYPE DEFAULT NULL
) RETURN NUMBER
IS
  v_uteis     PLS_INTEGER;
  v_avaliadas PLS_INTEGER;
BEGIN
  SELECT COUNT(CASE WHEN util = 1 THEN 1 END),
         COUNT(util)
    INTO v_uteis, v_avaliadas
    FROM descricao
   WHERE p_usuario_id IS NULL
      OR usuario_id = p_usuario_id;

  IF v_avaliadas = 0 THEN
    RETURN NULL;
  END IF;

  RETURN ROUND(v_uteis / v_avaliadas * 100, 2);
END fn_taxa_descricoes_uteis;
/


-- ----------------------------------------------------------------------------- 03_procedures.sql
-- =============================================================================
-- Vê AI · Oracle · 03 · Procedimentos
--   prc_registrar_relato_ou_apoio   regra dos 30 m: cria o relato ou registra +1 apoio
--   prc_atualizar_status            máquina de estados do relato + histórico + notificações
--
-- As procedures NÃO fazem COMMIT: quem chama decide (assim os testes
-- conseguem desfazer tudo com ROLLBACK).
--
-- Códigos de erro (também expostos como exceções em pkg_relatos):
--   -20001 transição de status inválida
--   -20002 gestor não atende a região do relato
--   -20003 protocolo do órgão obrigatório para ENCAMINHADO
--   -20004 motivo obrigatório para DESCARTADO
--   -20005 relato inexistente
--   -20006 usuário inexistente ou com papel incorreto
-- =============================================================================

-- -----------------------------------------------------------------------------
-- prc_registrar_relato_ou_apoio
--   Procura um relato NÃO FINALIZADO (ABERTO, EM_TRIAGEM ou ENCAMINHADO) do
--   mesmo tipo a até 30 m.
--     - não existe          -> cria o relato              p_resultado = 'CRIADO'
--     - existe, outro autor -> insere APOIO_RELATO         p_resultado = 'APOIO_REGISTRADO'
--       (o trg_conta_apoio soma +1 e recalcula a prioridade)
--     - existe, e quem envia é o autor ou já apoiou        p_resultado = 'JA_REGISTRADO'
--   Em todos os casos p_relato_id devolve o relato afetado.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE prc_registrar_relato_ou_apoio (
  p_cidadao_id   IN  relato.cidadao_id%TYPE,
  p_descricao_id IN  relato.descricao_id%TYPE,
  p_regiao_id    IN  relato.regiao_id%TYPE,
  p_tipo         IN  relato.tipo%TYPE,
  p_gravidade    IN  relato.gravidade%TYPE,
  p_texto        IN  relato.texto%TYPE,
  p_latitude     IN  relato.latitude%TYPE,
  p_longitude    IN  relato.longitude%TYPE,
  p_endereco     IN  relato.endereco%TYPE,
  p_relato_id    OUT relato.id%TYPE,
  p_resultado    OUT VARCHAR2
)
IS
  c_raio_metros CONSTANT NUMBER := 30;
  -- Pré-filtro (bounding box) para não calcular Haversine na tabela inteira:
  -- 1 grau de latitude ~ 111 km; folga de 50%.
  c_delta_lat   CONSTANT NUMBER := c_raio_metros / 111000 * 1.5;
  v_delta_lon   NUMBER;
  v_papel       usuario.papel%TYPE;
  v_autor_id    relato.cidadao_id%TYPE;
  v_ja_apoiou   PLS_INTEGER;
BEGIN
  -- 1. Só cidadão registra relato
  BEGIN
    SELECT papel INTO v_papel FROM usuario WHERE id = p_cidadao_id;
  EXCEPTION
    WHEN NO_DATA_FOUND THEN v_papel := NULL;
  END;

  IF v_papel IS NULL OR v_papel <> 'CIDADAO' THEN
    RAISE_APPLICATION_ERROR(-20006, 'Usuário ' || p_cidadao_id || ' não é um cidadão válido.');
  END IF;

  -- 2. Procura relato equivalente a até 30 m
  v_delta_lon := c_delta_lat / COS(p_latitude * ACOS(-1) / 180);

  BEGIN
    SELECT id, cidadao_id
      INTO p_relato_id, v_autor_id
      FROM (SELECT r.id,
                   r.cidadao_id,
                   fn_distancia_metros(p_latitude, p_longitude, r.latitude, r.longitude) AS distancia
              FROM relato r
             WHERE r.tipo = p_tipo
               AND r.status IN ('ABERTO', 'EM_TRIAGEM', 'ENCAMINHADO')
               AND r.latitude  BETWEEN p_latitude  - c_delta_lat AND p_latitude  + c_delta_lat
               AND r.longitude BETWEEN p_longitude - v_delta_lon AND p_longitude + v_delta_lon)
     WHERE distancia <= c_raio_metros
     ORDER BY distancia
     FETCH FIRST 1 ROW ONLY;
  EXCEPTION
    WHEN NO_DATA_FOUND THEN p_relato_id := NULL;
  END;

  -- 3. Decide: criar, apoiar ou ignorar
  IF p_relato_id IS NULL THEN
    INSERT INTO relato (cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto,
                        latitude, longitude, endereco, status, total_apoios, prioridade,
                        criado_em, atualizado_em)
    VALUES (p_cidadao_id, p_descricao_id, p_regiao_id, p_tipo, p_gravidade, p_texto,
            p_latitude, p_longitude, p_endereco, 'ABERTO', 0,
            fn_calcular_prioridade(p_gravidade, 0, SYSDATE, 'ABERTO'),
            SYSDATE, SYSDATE)
    RETURNING id INTO p_relato_id;

    p_resultado := 'CRIADO';

  ELSIF v_autor_id = p_cidadao_id THEN
    p_resultado := 'JA_REGISTRADO';

  ELSE
    SELECT COUNT(*) INTO v_ja_apoiou
      FROM apoio_relato
     WHERE relato_id = p_relato_id
       AND usuario_id = p_cidadao_id;

    IF v_ja_apoiou > 0 THEN
      p_resultado := 'JA_REGISTRADO';
    ELSE
      INSERT INTO apoio_relato (relato_id, usuario_id, criado_em)
      VALUES (p_relato_id, p_cidadao_id, SYSDATE);
      p_resultado := 'APOIO_REGISTRADO';
    END IF;
  END IF;
END prc_registrar_relato_ou_apoio;
/

-- -----------------------------------------------------------------------------
-- prc_atualizar_status
--   Transições válidas (sem pular etapa e sem voltar):
--     ABERTO      -> EM_TRIAGEM | DESCARTADO
--     EM_TRIAGEM  -> ENCAMINHADO | DESCARTADO
--     ENCAMINHADO -> RESOLVIDO | DESCARTADO
--     RESOLVIDO e DESCARTADO são finais.
--   Regras extras: ENCAMINHADO exige protocolo; DESCARTADO exige motivo;
--   o gestor precisa atender a região do relato (GESTOR_REGIAO).
--   Efeitos: atualiza RELATO (status, protocolo/motivo, prioridade),
--   grava ATUALIZACAO_RELATO e cria uma NOTIFICACAO para o autor e para cada
--   apoiador (na arquitetura real isso é feito pelo consumidor RabbitMQ da API).
-- -----------------------------------------------------------------------------
CREATE OR REPLACE PROCEDURE prc_atualizar_status (
  p_relato_id   IN relato.id%TYPE,
  p_gestor_id   IN usuario.id%TYPE,
  p_status_novo IN relato.status%TYPE,
  p_protocolo   IN relato.protocolo_orgao%TYPE        DEFAULT NULL,
  p_comentario  IN atualizacao_relato.comentario%TYPE DEFAULT NULL,
  p_motivo      IN relato.motivo_descarte%TYPE        DEFAULT NULL
)
IS
  v_status_atual relato.status%TYPE;
  v_regiao_id    relato.regiao_id%TYPE;
  v_vinculo      PLS_INTEGER;
  v_valida       BOOLEAN := FALSE;
  v_mensagem     notificacao.mensagem%TYPE;
  v_notificados  PLS_INTEGER := 0;

  -- Autor do relato + todos os apoiadores (UNION remove repetidos)
  CURSOR c_interessados IS
    SELECT cidadao_id AS usuario_id FROM relato WHERE id = p_relato_id
    UNION
    SELECT usuario_id FROM apoio_relato WHERE relato_id = p_relato_id;
BEGIN
  -- 1. Relato existe? (trava a linha até o fim da transação)
  BEGIN
    SELECT status, regiao_id
      INTO v_status_atual, v_regiao_id
      FROM relato
     WHERE id = p_relato_id
       FOR UPDATE;
  EXCEPTION
    WHEN NO_DATA_FOUND THEN
      RAISE_APPLICATION_ERROR(-20005, 'Relato ' || p_relato_id || ' não existe.');
  END;

  -- 2. Gestor atende a região do relato?
  SELECT COUNT(*)
    INTO v_vinculo
    FROM gestor_regiao gr
    JOIN usuario u ON u.id = gr.gestor_id
   WHERE gr.gestor_id = p_gestor_id
     AND gr.regiao_id = v_regiao_id
     AND u.papel = 'GESTOR';

  IF v_vinculo = 0 THEN
    RAISE_APPLICATION_ERROR(-20002, 'Gestor ' || p_gestor_id || ' não atende a região do relato '
                                    || p_relato_id || '.');
  END IF;

  -- 3. Transição permitida?
  IF v_status_atual = 'ABERTO' AND p_status_novo IN ('EM_TRIAGEM', 'DESCARTADO') THEN
    v_valida := TRUE;
  ELSIF v_status_atual = 'EM_TRIAGEM' AND p_status_novo IN ('ENCAMINHADO', 'DESCARTADO') THEN
    v_valida := TRUE;
  ELSIF v_status_atual = 'ENCAMINHADO' AND p_status_novo IN ('RESOLVIDO', 'DESCARTADO') THEN
    v_valida := TRUE;
  END IF;

  IF NOT v_valida THEN
    RAISE_APPLICATION_ERROR(-20001, 'Transição inválida: ' || v_status_atual || ' -> '
                                    || NVL(p_status_novo, '(nulo)') || '.');
  END IF;

  -- 4. Campos obrigatórios por status
  IF p_status_novo = 'ENCAMINHADO' AND TRIM(p_protocolo) IS NULL THEN
    RAISE_APPLICATION_ERROR(-20003, 'Informe o protocolo do órgão para encaminhar o relato.');
  END IF;

  IF p_status_novo = 'DESCARTADO' AND TRIM(p_motivo) IS NULL THEN
    RAISE_APPLICATION_ERROR(-20004, 'Informe o motivo do descarte.');
  END IF;

  -- 5. Atualiza o relato (o trg_auditoria_relato registra antigo/novo)
  UPDATE relato
     SET status          = p_status_novo,
         protocolo_orgao = CASE WHEN p_status_novo = 'ENCAMINHADO' THEN TRIM(p_protocolo)
                                ELSE protocolo_orgao END,
         motivo_descarte = CASE WHEN p_status_novo = 'DESCARTADO' THEN TRIM(p_motivo)
                                ELSE motivo_descarte END,
         prioridade      = fn_calcular_prioridade(gravidade, total_apoios, criado_em, p_status_novo),
         atualizado_em   = SYSDATE
   WHERE id = p_relato_id;

  -- 6. Histórico de negócio
  INSERT INTO atualizacao_relato (relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
  VALUES (p_relato_id, p_gestor_id, v_status_atual, p_status_novo, p_comentario, SYSDATE);

  -- 7. Notificações (lidas em voz alta pelo app)
  v_mensagem := 'O relato #' || p_relato_id || ' que você registrou ou apoiou '
             || CASE p_status_novo
                  WHEN 'EM_TRIAGEM'  THEN 'está em triagem pela rede de apoio.'
                  WHEN 'ENCAMINHADO' THEN 'foi encaminhado ao órgão responsável. Protocolo '
                                          || TRIM(p_protocolo) || '.'
                  WHEN 'RESOLVIDO'   THEN 'foi resolvido. Obrigado por ajudar a cidade!'
                  WHEN 'DESCARTADO'  THEN 'foi descartado. Motivo: ' || TRIM(p_motivo)
                END;

  FOR r IN c_interessados LOOP
    INSERT INTO notificacao (usuario_id, relato_id, mensagem, lida, criado_em)
    VALUES (r.usuario_id, p_relato_id, SUBSTR(v_mensagem, 1, 300), 0, SYSDATE);
    v_notificados := v_notificados + 1;
  END LOOP;

  DBMS_OUTPUT.PUT_LINE('Relato ' || p_relato_id || ': ' || v_status_atual || ' -> ' || p_status_novo
                       || ' (' || v_notificados || ' notificação(ões) criada(s))');
END prc_atualizar_status;
/


-- ----------------------------------------------------------------------------- 04_triggers.sql
-- =============================================================================
-- Vê AI · Oracle · 04 · Gatilhos
--   trg_auditoria_relato   auditoria de INSERT/UPDATE/DELETE em RELATO
--                          (data, operação, usuário, valor antigo e novo)
--   trg_conta_apoio        mantém RELATO.total_apoios e a prioridade quando um
--                          apoio é inserido ou removido
-- =============================================================================

-- -----------------------------------------------------------------------------
-- trg_auditoria_relato
--   :OLD é nulo no INSERT e :NEW é nulo no DELETE, então status_antigo /
--   status_novo ficam vazios exatamente onde não existem.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE TRIGGER trg_auditoria_relato
AFTER INSERT OR UPDATE OR DELETE ON relato
FOR EACH ROW
DECLARE
  v_operacao auditoria_relato.operacao%TYPE;
BEGIN
  IF INSERTING THEN
    v_operacao := 'INSERT';
  ELSIF UPDATING THEN
    v_operacao := 'UPDATE';
  ELSE
    v_operacao := 'DELETE';
  END IF;

  INSERT INTO auditoria_relato (relato_id, operacao, usuario_bd, data_operacao,
                                status_antigo, status_novo,
                                prioridade_antiga, prioridade_nova)
  VALUES (NVL(:NEW.id, :OLD.id), v_operacao,
          SYS_CONTEXT('USERENV', 'SESSION_USER'), SYSTIMESTAMP,
          :OLD.status, :NEW.status,
          :OLD.prioridade, :NEW.prioridade);
END trg_auditoria_relato;
/

-- -----------------------------------------------------------------------------
-- trg_conta_apoio
--   Atualiza OUTRA tabela (RELATO), então não há erro de tabela mutante.
--   No SET, "total_apoios" à direita é o valor ANTES do update, por isso a
--   prioridade é calculada com total_apoios + 1 / - 1.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE TRIGGER trg_conta_apoio
AFTER INSERT OR DELETE ON apoio_relato
FOR EACH ROW
BEGIN
  IF INSERTING THEN
    UPDATE relato
       SET total_apoios = total_apoios + 1,
           prioridade   = fn_calcular_prioridade(gravidade, total_apoios + 1, criado_em, status)
     WHERE id = :NEW.relato_id;
  ELSE
    UPDATE relato
       SET total_apoios = GREATEST(total_apoios - 1, 0),
           prioridade   = fn_calcular_prioridade(gravidade, GREATEST(total_apoios - 1, 0),
                                                 criado_em, status)
     WHERE id = :OLD.relato_id;
  END IF;
END trg_conta_apoio;
/


-- ----------------------------------------------------------------------------- 05_pacotes.sql
-- =============================================================================
-- Vê AI · Oracle · 05 · Pacotes
--   pkg_relatos      fila do gestor: prioridade, registro, status, atrasados
--   pkg_descricoes   métricas de qualidade da IA: utilidade, latência, barreiras
--
-- Os objetos avulsos (fn_/prc_) contêm a implementação das regras exigidas no
-- critério 02. Os pacotes expõem essas regras com uma API única, acrescentam
-- rotinas próprias e declaram exceções nomeadas para cada código de erro.
-- =============================================================================

-- =============================================================================
-- pkg_relatos · SPEC
-- =============================================================================
CREATE OR REPLACE PACKAGE pkg_relatos AS

  e_transicao_invalida    EXCEPTION;
  e_gestor_fora_regiao    EXCEPTION;
  e_protocolo_obrigatorio EXCEPTION;
  e_motivo_obrigatorio    EXCEPTION;
  e_relato_inexistente    EXCEPTION;
  e_usuario_invalido      EXCEPTION;

  PRAGMA EXCEPTION_INIT(e_transicao_invalida,    -20001);
  PRAGMA EXCEPTION_INIT(e_gestor_fora_regiao,    -20002);
  PRAGMA EXCEPTION_INIT(e_protocolo_obrigatorio, -20003);
  PRAGMA EXCEPTION_INIT(e_motivo_obrigatorio,    -20004);
  PRAGMA EXCEPTION_INIT(e_relato_inexistente,    -20005);
  PRAGMA EXCEPTION_INIT(e_usuario_invalido,      -20006);

  -- Relato não finalizado criado há mais de N dias é considerado "atrasado"
  c_dias_atraso CONSTANT PLS_INTEGER := 30;

  -- Prioridade atual do relato (delegada a fn_prioridade_relato)
  FUNCTION prioridade (p_relato_id IN relato.id%TYPE) RETURN NUMBER;

  -- Regra dos 30 m (delegada a prc_registrar_relato_ou_apoio)
  PROCEDURE registrar (
    p_cidadao_id   IN  relato.cidadao_id%TYPE,
    p_descricao_id IN  relato.descricao_id%TYPE,
    p_regiao_id    IN  relato.regiao_id%TYPE,
    p_tipo         IN  relato.tipo%TYPE,
    p_gravidade    IN  relato.gravidade%TYPE,
    p_texto        IN  relato.texto%TYPE,
    p_latitude     IN  relato.latitude%TYPE,
    p_longitude    IN  relato.longitude%TYPE,
    p_endereco     IN  relato.endereco%TYPE,
    p_relato_id    OUT relato.id%TYPE,
    p_resultado    OUT VARCHAR2
  );

  -- Máquina de estados (delegada a prc_atualizar_status)
  PROCEDURE atualizar_status (
    p_relato_id   IN relato.id%TYPE,
    p_gestor_id   IN usuario.id%TYPE,
    p_status_novo IN relato.status%TYPE,
    p_protocolo   IN relato.protocolo_orgao%TYPE        DEFAULT NULL,
    p_comentario  IN atualizacao_relato.comentario%TYPE DEFAULT NULL,
    p_motivo      IN relato.motivo_descarte%TYPE        DEFAULT NULL
  );

  -- Recalcula a prioridade de todos os relatos (os dias em aberto mudam todo dia).
  -- Devolve quantos relatos tiveram a prioridade alterada. Não faz COMMIT.
  PROCEDURE recalcular_prioridades (p_qtd_alterados OUT PLS_INTEGER);

  -- Relatos não finalizados criados há mais de p_dias, do mais antigo ao mais novo
  FUNCTION relatos_atrasados (p_dias IN PLS_INTEGER DEFAULT c_dias_atraso) RETURN SYS_REFCURSOR;

END pkg_relatos;
/

-- =============================================================================
-- pkg_relatos · BODY
-- =============================================================================
CREATE OR REPLACE PACKAGE BODY pkg_relatos AS

  FUNCTION prioridade (p_relato_id IN relato.id%TYPE) RETURN NUMBER IS
  BEGIN
    RETURN fn_prioridade_relato(p_relato_id);
  END prioridade;

  PROCEDURE registrar (
    p_cidadao_id   IN  relato.cidadao_id%TYPE,
    p_descricao_id IN  relato.descricao_id%TYPE,
    p_regiao_id    IN  relato.regiao_id%TYPE,
    p_tipo         IN  relato.tipo%TYPE,
    p_gravidade    IN  relato.gravidade%TYPE,
    p_texto        IN  relato.texto%TYPE,
    p_latitude     IN  relato.latitude%TYPE,
    p_longitude    IN  relato.longitude%TYPE,
    p_endereco     IN  relato.endereco%TYPE,
    p_relato_id    OUT relato.id%TYPE,
    p_resultado    OUT VARCHAR2
  ) IS
  BEGIN
    prc_registrar_relato_ou_apoio(p_cidadao_id, p_descricao_id, p_regiao_id, p_tipo, p_gravidade,
                                  p_texto, p_latitude, p_longitude, p_endereco,
                                  p_relato_id, p_resultado);
  END registrar;

  PROCEDURE atualizar_status (
    p_relato_id   IN relato.id%TYPE,
    p_gestor_id   IN usuario.id%TYPE,
    p_status_novo IN relato.status%TYPE,
    p_protocolo   IN relato.protocolo_orgao%TYPE        DEFAULT NULL,
    p_comentario  IN atualizacao_relato.comentario%TYPE DEFAULT NULL,
    p_motivo      IN relato.motivo_descarte%TYPE        DEFAULT NULL
  ) IS
  BEGIN
    prc_atualizar_status(p_relato_id, p_gestor_id, p_status_novo, p_protocolo, p_comentario, p_motivo);
  END atualizar_status;

  PROCEDURE recalcular_prioridades (p_qtd_alterados OUT PLS_INTEGER) IS
    v_nova relato.prioridade%TYPE;
  BEGIN
    p_qtd_alterados := 0;

    FOR r IN (SELECT id, gravidade, total_apoios, criado_em, status, prioridade
                FROM relato
                 FOR UPDATE OF prioridade)
    LOOP
      v_nova := fn_calcular_prioridade(r.gravidade, r.total_apoios, r.criado_em, r.status);

      IF v_nova <> r.prioridade THEN
        UPDATE relato SET prioridade = v_nova WHERE id = r.id;
        p_qtd_alterados := p_qtd_alterados + 1;
      END IF;
    END LOOP;
  END recalcular_prioridades;

  FUNCTION relatos_atrasados (p_dias IN PLS_INTEGER DEFAULT c_dias_atraso) RETURN SYS_REFCURSOR IS
    v_cursor SYS_REFCURSOR;
  BEGIN
    OPEN v_cursor FOR
      SELECT r.id,
             g.nome                      AS regiao,
             r.tipo,
             r.gravidade,
             r.status,
             FLOOR(SYSDATE - r.criado_em) AS dias_em_aberto,
             r.total_apoios
        FROM relato r
        JOIN regiao g ON g.id = r.regiao_id
       WHERE r.status IN ('ABERTO', 'EM_TRIAGEM', 'ENCAMINHADO')
         AND r.criado_em < SYSDATE - p_dias
       ORDER BY r.criado_em, r.id;
    RETURN v_cursor;
  END relatos_atrasados;

END pkg_relatos;
/

-- =============================================================================
-- pkg_descricoes · SPEC
-- =============================================================================
CREATE OR REPLACE PACKAGE pkg_descricoes AS

  e_descricao_inexistente EXCEPTION;
  e_avaliacao_invalida    EXCEPTION;
  PRAGMA EXCEPTION_INIT(e_descricao_inexistente, -20007);
  PRAGMA EXCEPTION_INIT(e_avaliacao_invalida,    -20008);

  -- % de descrições úteis entre as avaliadas (delegada a fn_taxa_descricoes_uteis)
  FUNCTION taxa_utilidade (p_usuario_id IN usuario.id%TYPE DEFAULT NULL) RETURN NUMBER;

  -- Latência média da IA em ms, com filtros opcionais por usuário e modo
  FUNCTION latencia_media (
    p_usuario_id IN usuario.id%TYPE      DEFAULT NULL,
    p_modo       IN descricao.modo%TYPE  DEFAULT NULL
  ) RETURN NUMBER;

  -- Quantas descrições tiveram barreira estrutural detectada pela IA
  FUNCTION total_barreiras_detectadas (p_usuario_id IN usuario.id%TYPE DEFAULT NULL) RETURN PLS_INTEGER;

  -- Avaliação do cidadão: 1 = útil, 0 = não útil, NULL = limpa a avaliação
  PROCEDURE avaliar (p_descricao_id IN descricao.id%TYPE, p_util IN NUMBER);

  -- Resumo de uso de um cidadão
  PROCEDURE estatisticas_usuario (
    p_usuario_id     IN  usuario.id%TYPE,
    p_total          OUT PLS_INTEGER,
    p_ambiente       OUT PLS_INTEGER,
    p_texto          OUT PLS_INTEGER,
    p_taxa_util      OUT NUMBER,
    p_latencia_media OUT NUMBER
  );

END pkg_descricoes;
/

-- =============================================================================
-- pkg_descricoes · BODY
-- =============================================================================
CREATE OR REPLACE PACKAGE BODY pkg_descricoes AS

  FUNCTION taxa_utilidade (p_usuario_id IN usuario.id%TYPE DEFAULT NULL) RETURN NUMBER IS
  BEGIN
    RETURN fn_taxa_descricoes_uteis(p_usuario_id);
  END taxa_utilidade;

  FUNCTION latencia_media (
    p_usuario_id IN usuario.id%TYPE      DEFAULT NULL,
    p_modo       IN descricao.modo%TYPE  DEFAULT NULL
  ) RETURN NUMBER IS
    v_media NUMBER;
  BEGIN
    SELECT ROUND(AVG(latencia_ms))
      INTO v_media
      FROM descricao
     WHERE (p_usuario_id IS NULL OR usuario_id = p_usuario_id)
       AND (p_modo IS NULL OR modo = p_modo);
    RETURN v_media;
  END latencia_media;

  FUNCTION total_barreiras_detectadas (p_usuario_id IN usuario.id%TYPE DEFAULT NULL) RETURN PLS_INTEGER IS
    v_total PLS_INTEGER;
  BEGIN
    SELECT COUNT(*)
      INTO v_total
      FROM descricao
     WHERE barreira_detectada = 1
       AND (p_usuario_id IS NULL OR usuario_id = p_usuario_id);
    RETURN v_total;
  END total_barreiras_detectadas;

  PROCEDURE avaliar (p_descricao_id IN descricao.id%TYPE, p_util IN NUMBER) IS
  BEGIN
    IF p_util IS NOT NULL AND p_util NOT IN (0, 1) THEN
      RAISE_APPLICATION_ERROR(-20008, 'Avaliação inválida: use 1 (útil), 0 (não útil) ou NULL.');
    END IF;

    UPDATE descricao SET util = p_util WHERE id = p_descricao_id;

    IF SQL%ROWCOUNT = 0 THEN
      RAISE_APPLICATION_ERROR(-20007, 'Descrição ' || p_descricao_id || ' não existe.');
    END IF;
  END avaliar;

  PROCEDURE estatisticas_usuario (
    p_usuario_id     IN  usuario.id%TYPE,
    p_total          OUT PLS_INTEGER,
    p_ambiente       OUT PLS_INTEGER,
    p_texto          OUT PLS_INTEGER,
    p_taxa_util      OUT NUMBER,
    p_latencia_media OUT NUMBER
  ) IS
    v_existe PLS_INTEGER;
  BEGIN
    SELECT COUNT(*) INTO v_existe FROM usuario WHERE id = p_usuario_id;
    IF v_existe = 0 THEN
      RAISE_APPLICATION_ERROR(-20006, 'Usuário ' || p_usuario_id || ' não existe.');
    END IF;

    SELECT COUNT(*),
           COUNT(CASE WHEN modo = 'AMBIENTE' THEN 1 END),
           COUNT(CASE WHEN modo = 'TEXTO' THEN 1 END)
      INTO p_total, p_ambiente, p_texto
      FROM descricao
     WHERE usuario_id = p_usuario_id;

    p_taxa_util      := taxa_utilidade(p_usuario_id);
    p_latencia_media := latencia_media(p_usuario_id);
  END estatisticas_usuario;

END pkg_descricoes;
/


