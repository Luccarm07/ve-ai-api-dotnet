-- =============================================================================
-- Vê AI · Oracle · Seed de demonstração (migração repetível do Flyway)
-- Mesmos dados e IDs do repositório database (oracle/06_seed.sql, gerado de
-- tools/dataset.json): 3 regiões, 4 cidadãos, 2 gestores, 20 descrições, 15 relatos,
-- 12 apoios, 13 atualizações e 21 notificações. Datas relativas a SYSDATE.
-- Senha de todos os usuários: Veai@2026
--
-- 1. Só insere se a tabela USUARIO estiver vazia (Oracle novo do docker-compose).
-- 2. Se os dados vieram do 06_seed.sql (hash ilustrativo, que não permite login),
--    troca o hash pela senha Veai@2026 para os usuários de teste poderem logar na API.
-- =============================================================================
DECLARE
  v_qtd PLS_INTEGER;
BEGIN
  SELECT COUNT(*) INTO v_qtd FROM usuario;
  IF v_qtd = 0 THEN
    -- REGIAO
    INSERT INTO regiao (id, nome, cidade, uf, latitude, longitude) VALUES (1, 'Pinheiros', 'São Paulo', 'SP', -23.566200, -46.689000);
    INSERT INTO regiao (id, nome, cidade, uf, latitude, longitude) VALUES (2, 'Sé', 'São Paulo', 'SP', -23.550300, -46.633900);
    INSERT INTO regiao (id, nome, cidade, uf, latitude, longitude) VALUES (3, 'Vila Mariana', 'São Paulo', 'SP', -23.588900, -46.635500);

    -- USUARIO (senha Veai@2026)
    INSERT INTO usuario (id, nome, email, senha_hash, papel, velocidade_voz, pergunta_livre_ativa, criado_em)
      VALUES (1, 'Ana Souza', 'ana.souza@veai.dev', '$2a$10$xCHtq1/BEvjc2Bwk9iQ37O84OOuuVQPqIF/pYmbI0hxqFP3jXuOBi', 'CIDADAO', 1.25, 1, SYSDATE - 90);
    INSERT INTO usuario (id, nome, email, senha_hash, papel, velocidade_voz, pergunta_livre_ativa, criado_em)
      VALUES (2, 'Bruno Lima', 'bruno.lima@veai.dev', '$2a$10$xCHtq1/BEvjc2Bwk9iQ37O84OOuuVQPqIF/pYmbI0hxqFP3jXuOBi', 'CIDADAO', 1, 0, SYSDATE - 85);
    INSERT INTO usuario (id, nome, email, senha_hash, papel, velocidade_voz, pergunta_livre_ativa, criado_em)
      VALUES (3, 'Carla Mendes', 'carla.mendes@veai.dev', '$2a$10$xCHtq1/BEvjc2Bwk9iQ37O84OOuuVQPqIF/pYmbI0hxqFP3jXuOBi', 'CIDADAO', 1.5, 1, SYSDATE - 80);
    INSERT INTO usuario (id, nome, email, senha_hash, papel, velocidade_voz, pergunta_livre_ativa, criado_em)
      VALUES (4, 'Diego Rocha', 'diego.rocha@veai.dev', '$2a$10$xCHtq1/BEvjc2Bwk9iQ37O84OOuuVQPqIF/pYmbI0hxqFP3jXuOBi', 'CIDADAO', 0.9, 0, SYSDATE - 70);
    INSERT INTO usuario (id, nome, email, senha_hash, papel, velocidade_voz, pergunta_livre_ativa, criado_em)
      VALUES (5, 'Marina Alves', 'marina.alves@ong-veai.dev', '$2a$10$xCHtq1/BEvjc2Bwk9iQ37O84OOuuVQPqIF/pYmbI0hxqFP3jXuOBi', 'GESTOR', 1, 0, SYSDATE - 100);
    INSERT INTO usuario (id, nome, email, senha_hash, papel, velocidade_voz, pergunta_livre_ativa, criado_em)
      VALUES (6, 'Rafael Costa', 'rafael.costa@cras-veai.dev', '$2a$10$xCHtq1/BEvjc2Bwk9iQ37O84OOuuVQPqIF/pYmbI0hxqFP3jXuOBi', 'GESTOR', 1, 0, SYSDATE - 100);

    -- GESTOR_REGIAO
    INSERT INTO gestor_regiao (gestor_id, regiao_id) VALUES (5, 1);
    INSERT INTO gestor_regiao (gestor_id, regiao_id) VALUES (5, 2);
    INSERT INTO gestor_regiao (gestor_id, regiao_id) VALUES (6, 3);

    -- DESCRICAO (perigos/objetos = JSON devolvido pela IA)
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (1, 1, 'AMBIENTE', 'Calçada com buraco grande à frente.',
              'Calçada de concreto com um buraco de cerca de 40 cm a dois passos à frente. À direita há um poste e, à esquerda, uma árvore com raízes expostas.',
              '[{"descricao":"buraco na calçada","direcao":"frente","distanciaAprox":"2 m"}]',
              '["poste","árvore"]',
              1, 'CALCADA_DANIFICADA', 2300, 1, SYSDATE - 60);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (2, 1, 'TEXTO', 'Placa: Farmácia aberta 24 horas.',
              'Placa luminosa na fachada com o texto: Farmácia aberta 24 horas.',
              '[]',
              '["placa"]',
              0, NULL, 1900, 1, SYSDATE - 58);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (3, 2, 'AMBIENTE', 'Calçada obstruída por uma banca fixa.',
              'Uma banca de jornal fixa ocupa quase toda a largura da calçada, deixando uma passagem estreita junto ao meio-fio.',
              '[{"descricao":"banca de jornal","direcao":"frente","distanciaAprox":"1 m"}]',
              '["banca de jornal","poste","meio-fio"]',
              1, 'OBSTRUCAO_PERMANENTE', 2800, 1, SYSDATE - 50);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (4, 2, 'AMBIENTE', 'Rampa de acesso termina em degrau.',
              'Rampa de acesso à faixa de pedestres que termina em um degrau de aproximadamente 10 cm antes da rua.',
              '[{"descricao":"degrau no fim da rampa","direcao":"frente","distanciaAprox":"3 m"}]',
              '["rampa","faixa de pedestres"]',
              1, 'RAMPA', 3100, 0, SYSDATE - 25);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (5, 3, 'AMBIENTE', 'Piso tátil interrompido por um canteiro.',
              'A linha de piso tátil direcional termina em um canteiro de plantas no meio da praça.',
              '[{"descricao":"canteiro elevado","direcao":"frente","distanciaAprox":"2 m"}]',
              '["piso tátil","canteiro","banco"]',
              1, 'PISO_TATIL', 2500, 1, SYSDATE - 25);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (6, 3, 'TEXTO', 'Cardápio: prato do dia, frango grelhado.',
              'Quadro de giz com o texto: Prato do dia, frango grelhado com arroz e salada.',
              '[]',
              '["quadro de giz"]',
              0, NULL, 2100, NULL, SYSDATE - 44);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (7, 4, 'AMBIENTE', 'Obra na calçada sem sinalização.',
              'Vala aberta na calçada, sem tapume nem fita de sinalização, ocupando metade da passagem.',
              '[{"descricao":"vala aberta","direcao":"esquerda","distanciaAprox":"1,5 m"}]',
              '["vala","terra","cone caído"]',
              1, 'OBRA_SEM_SINALIZACAO', 3400, 1, SYSDATE - 40);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (8, 1, 'AMBIENTE', 'Faixa de pedestres com carro se aproximando.',
              'Faixa de pedestres à frente. Um carro se aproxima pela direita. Aguarde antes de atravessar.',
              '[{"descricao":"carro em movimento","direcao":"direita","distanciaAprox":"5 m"}]',
              '["faixa de pedestres","carro","semáforo"]',
              0, NULL, 2600, 1, SYSDATE - 35);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (9, 4, 'TEXTO', 'Letreiro do ônibus: 875A Lapa.',
              'Letreiro do ônibus parcialmente legível: 875A Lapa.',
              '[]',
              '["ônibus","letreiro"]',
              0, NULL, 1800, 0, SYSDATE - 31);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (10, 2, 'AMBIENTE', 'Calçada estreita com poste à direita.',
              'Calçada estreita, com um poste à direita a um passo e uma lixeira logo depois.',
              '[{"descricao":"poste","direcao":"direita","distanciaAprox":"1 m"}]',
              '["poste","lixeira"]',
              0, NULL, 2200, 1, SYSDATE - 26);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (11, 3, 'AMBIENTE', 'Calçada quebrada perto do ponto de ônibus.',
              'Placas de concreto soltas e desniveladas na calçada, logo antes do ponto de ônibus.',
              '[{"descricao":"piso desnivelado","direcao":"frente","distanciaAprox":"1 m"}]',
              '["ponto de ônibus","placas de concreto"]',
              1, 'CALCADA_DANIFICADA', 2900, 1, SYSDATE - 20);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (12, 1, 'AMBIENTE', 'A foto está escura. Não é possível descrever com segurança.',
              'A imagem está muito escura. Tente novamente com mais luz ou aproxime o celular.',
              '[]',
              '[]',
              0, NULL, 1700, 0, SYSDATE - 18);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (13, 4, 'AMBIENTE', 'Calçada obstruída por entulho junto ao muro.',
              'Pilha de entulho encostada no muro ocupa boa parte da calçada.',
              '[{"descricao":"entulho","direcao":"frente","distanciaAprox":"2 m"}]',
              '["entulho","muro"]',
              1, 'OBSTRUCAO_PERMANENTE', 3000, NULL, SYSDATE - 15);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (14, 2, 'TEXTO', 'Aviso: elevador em manutenção.',
              'Folha colada na porta: Elevador em manutenção. Use a escada à esquerda.',
              '[]',
              '["aviso","porta do elevador"]',
              0, NULL, 2000, 1, SYSDATE - 12);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (15, 3, 'AMBIENTE', 'Rampa muito inclinada e sem corrimão.',
              'Rampa de acesso com inclinação acentuada e sem corrimão em nenhum dos lados.',
              '[{"descricao":"rampa íngreme","direcao":"frente","distanciaAprox":"2 m"}]',
              '["rampa"]',
              1, 'RAMPA', 3300, 1, SYSDATE - 10);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (16, 1, 'AMBIENTE', 'Bicicleta vindo pela calçada.',
              'Uma bicicleta se aproxima pela calçada, de frente. Mantenha-se à direita.',
              '[{"descricao":"bicicleta em movimento","direcao":"frente","distanciaAprox":"4 m"}]',
              '["bicicleta","calçada"]',
              0, NULL, 2400, 1, SYSDATE - 7);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (17, 4, 'AMBIENTE', 'Travessia sem piso tátil.',
              'Travessia de pedestres sem piso tátil de alerta no rebaixamento da calçada.',
              '[{"descricao":"rebaixamento sem alerta tátil","direcao":"frente","distanciaAprox":"1 m"}]',
              '["faixa de pedestres","guia rebaixada"]',
              1, 'PISO_TATIL', 2700, 0, SYSDATE - 5);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (18, 2, 'TEXTO', 'Placa de rua: Rua Augusta.',
              'Placa de rua azul com o texto: Rua Augusta.',
              '[]',
              '["placa de rua"]',
              0, NULL, 1600, NULL, SYSDATE - 3);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (19, 3, 'AMBIENTE', 'Calçada larga; atenção ao meio-fio à esquerda.',
              'Calçada larga e plana. O meio-fio fica à esquerda, a cerca de um metro. Não identifiquei outros obstáculos próximos, mas não posso garantir que o caminho esteja livre.',
              '[{"descricao":"meio-fio","direcao":"esquerda","distanciaAprox":"1 m"}]',
              '["meio-fio","calçada"]',
              0, NULL, 2300, NULL, SYSDATE - 2);
    INSERT INTO descricao (id, usuario_id, modo, resumo, descricao_completa, perigos, objetos, barreira_detectada, barreira_tipo, latencia_ms, util, criado_em)
      VALUES (20, 1, 'AMBIENTE', 'Buraco profundo na calçada ao lado da árvore.',
              'Buraco profundo na calçada, junto às raízes de uma árvore, à frente e um pouco à esquerda.',
              '[{"descricao":"buraco profundo","direcao":"frente-esquerda","distanciaAprox":"2 m"}]',
              '["árvore","buraco"]',
              1, 'CALCADA_DANIFICADA', 2600, NULL, SYSDATE - 1);

    -- RELATO (total_apoios e prioridade começam em 0; o trigger e o UPDATE final ajustam)
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (1, 1, 1, 1, 'CALCADA_DANIFICADA', 'ALTA',
              'Buraco grande na calçada, em frente ao número 500.', -23.566120, -46.684310, 'Rua dos Pinheiros, 500',
              'ABERTO', NULL, NULL, 0, 0, NULL,
              SYSDATE - 60, SYSDATE - 60);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (2, 2, 3, 1, 'OBSTRUCAO_PERMANENTE', 'MEDIA',
              'Banca fixa ocupa quase toda a calçada.', -23.561450, -46.682540, 'Rua Teodoro Sampaio, 1200',
              'EM_TRIAGEM', NULL, NULL, 0, 0, NULL,
              SYSDATE - 50, SYSDATE - 20);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (3, 2, 4, 3, 'RAMPA', 'MEDIA',
              'Rampa da faixa de pedestres termina em degrau de 10 cm.', -23.588900, -46.634200, 'Rua Domingos de Morais, 2000',
              'ENCAMINHADO', 'SP156-2026-0012345', NULL, 0, 0, 'ABNT NBR 9050:2020, seção 6.6 (rampas)',
              SYSDATE - 25, SYSDATE - 18);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (4, 3, 5, 2, 'PISO_TATIL', 'ALTA',
              'Piso tátil interrompido por canteiro na praça.', -23.550520, -46.633310, 'Praça da Sé, s/n',
              'ABERTO', NULL, NULL, 0, 0, 'ABNT NBR 16537:2016 (sinalização tátil no piso)',
              SYSDATE - 25, SYSDATE - 25);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (5, 4, 7, 2, 'OBRA_SEM_SINALIZACAO', 'ALTA',
              'Vala aberta na calçada sem tapume.', -23.547800, -46.636100, 'Rua Direita, 150',
              'RESOLVIDO', 'SP156-2026-0013001', NULL, 0, 0, NULL,
              SYSDATE - 40, SYSDATE - 10);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (6, 1, NULL, 1, 'SEMAFORO_SEM_SOM', 'MEDIA',
              'Semáforo de pedestres sem sinal sonoro.', -23.567300, -46.693800, 'Av. Pedroso de Morais, 300',
              'ABERTO', NULL, NULL, 0, 0, NULL,
              SYSDATE - 38, SYSDATE - 38);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (7, 3, 11, 3, 'CALCADA_DANIFICADA', 'MEDIA',
              'Calçada quebrada perto do ponto de ônibus.', -23.586200, -46.637700, 'Rua Vergueiro, 3100',
              'EM_TRIAGEM', NULL, NULL, 0, 0, NULL,
              SYSDATE - 20, SYSDATE - 15);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (8, 4, 13, 2, 'OBSTRUCAO_PERMANENTE', 'BAIXA',
              'Entulho fixo junto ao muro.', -23.545900, -46.634900, 'Rua XV de Novembro, 80',
              'DESCARTADO', NULL, 'Entulho removido antes da vistoria.', 0, 0, NULL,
              SYSDATE - 15, SYSDATE - 8);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (9, 3, 15, 3, 'RAMPA', 'ALTA',
              'Rampa muito inclinada e sem corrimão.', -23.590400, -46.636500, 'Rua Domingos de Morais, 2600',
              'ABERTO', NULL, NULL, 0, 0, NULL,
              SYSDATE - 10, SYSDATE - 10);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (10, 4, 17, 1, 'PISO_TATIL', 'MEDIA',
              'Travessia sem piso tátil de alerta.', -23.564800, -46.690200, 'Rua Cardeal Arcoverde, 900',
              'ABERTO', NULL, NULL, 0, 0, NULL,
              SYSDATE - 5, SYSDATE - 5);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (11, 2, NULL, 2, 'OBRA_SEM_SINALIZACAO', 'ALTA',
              'Nova vala aberta no mesmo trecho, sem sinalização.', -23.547900, -46.636150, 'Rua Direita, 160',
              'ABERTO', NULL, NULL, 0, 0, NULL,
              SYSDATE - 4, SYSDATE - 4);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (12, 2, NULL, 3, 'OUTRO', 'BAIXA',
              'Lixeira instalada no meio da faixa livre da calçada.', -23.587500, -46.632900, 'Rua Joaquim Távora, 400',
              'ABERTO', NULL, NULL, 0, 0, NULL,
              SYSDATE - 20, SYSDATE - 20);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (13, 1, NULL, 2, 'SEMAFORO_SEM_SOM', 'MEDIA',
              'Botoeira sonora do semáforo quebrada.', -23.549300, -46.638900, 'Rua Líbero Badaró, 400',
              'ENCAMINHADO', 'SP156-2026-0009876', NULL, 0, 0, NULL,
              SYSDATE - 62, SYSDATE - 50);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (14, 4, NULL, 3, 'CALCADA_DANIFICADA', 'BAIXA',
              'Calçada com desnível pequeno entre placas.', -23.591100, -46.633800, 'Rua Vergueiro, 3800',
              'RESOLVIDO', 'SP156-2026-0014500', NULL, 0, 0, NULL,
              SYSDATE - 28, SYSDATE - 6);
    INSERT INTO relato (id, cidadao_id, descricao_id, regiao_id, tipo, gravidade, texto, latitude, longitude, endereco,
                        status, protocolo_orgao, motivo_descarte, total_apoios, prioridade, norma_referencia, criado_em, atualizado_em)
      VALUES (15, 3, NULL, 1, 'OUTRO', 'MEDIA',
              'Grade de proteção de árvore solta na calçada.', -23.563900, -46.688700, 'Rua Fradique Coutinho, 700',
              'ABERTO', NULL, NULL, 0, 0, NULL,
              SYSDATE - 2, SYSDATE - 2);

    -- APOIO_RELATO (cada INSERT dispara trg_conta_apoio: total_apoios + 1)
    INSERT INTO apoio_relato (id, relato_id, usuario_id, criado_em) VALUES (1, 1, 2, SYSDATE - 55);
    INSERT INTO apoio_relato (id, relato_id, usuario_id, criado_em) VALUES (2, 1, 3, SYSDATE - 40);
    INSERT INTO apoio_relato (id, relato_id, usuario_id, criado_em) VALUES (3, 2, 4, SYSDATE - 30);
    INSERT INTO apoio_relato (id, relato_id, usuario_id, criado_em) VALUES (4, 4, 1, SYSDATE - 24);
    INSERT INTO apoio_relato (id, relato_id, usuario_id, criado_em) VALUES (5, 4, 2, SYSDATE - 22);
    INSERT INTO apoio_relato (id, relato_id, usuario_id, criado_em) VALUES (6, 4, 4, SYSDATE - 21);
    INSERT INTO apoio_relato (id, relato_id, usuario_id, criado_em) VALUES (7, 5, 1, SYSDATE - 39);
    INSERT INTO apoio_relato (id, relato_id, usuario_id, criado_em) VALUES (8, 6, 3, SYSDATE - 20);
    INSERT INTO apoio_relato (id, relato_id, usuario_id, criado_em) VALUES (9, 10, 2, SYSDATE - 3);
    INSERT INTO apoio_relato (id, relato_id, usuario_id, criado_em) VALUES (10, 11, 4, SYSDATE - 2);
    INSERT INTO apoio_relato (id, relato_id, usuario_id, criado_em) VALUES (11, 13, 3, SYSDATE - 60);
    INSERT INTO apoio_relato (id, relato_id, usuario_id, criado_em) VALUES (12, 13, 4, SYSDATE - 59);

    -- ATUALIZACAO_RELATO (histórico de status feito pelos gestores)
    INSERT INTO atualizacao_relato (id, relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
      VALUES (1, 2, 5, 'ABERTO', 'EM_TRIAGEM', 'Em análise pela equipe da ONG.', SYSDATE - 20);
    INSERT INTO atualizacao_relato (id, relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
      VALUES (2, 3, 6, 'ABERTO', 'EM_TRIAGEM', 'Vistoria agendada.', SYSDATE - 22);
    INSERT INTO atualizacao_relato (id, relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
      VALUES (3, 3, 6, 'EM_TRIAGEM', 'ENCAMINHADO', 'Encaminhado à subprefeitura via SP156.', SYSDATE - 18);
    INSERT INTO atualizacao_relato (id, relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
      VALUES (4, 5, 5, 'ABERTO', 'EM_TRIAGEM', 'Prioridade alta: risco de queda.', SYSDATE - 38);
    INSERT INTO atualizacao_relato (id, relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
      VALUES (5, 5, 5, 'EM_TRIAGEM', 'ENCAMINHADO', 'Encaminhado à concessionária responsável.', SYSDATE - 37);
    INSERT INTO atualizacao_relato (id, relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
      VALUES (6, 5, 5, 'ENCAMINHADO', 'RESOLVIDO', 'Obra sinalizada e vala fechada.', SYSDATE - 10);
    INSERT INTO atualizacao_relato (id, relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
      VALUES (7, 7, 6, 'ABERTO', 'EM_TRIAGEM', 'Aguardando vistoria.', SYSDATE - 15);
    INSERT INTO atualizacao_relato (id, relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
      VALUES (8, 8, 5, 'ABERTO', 'DESCARTADO', 'Vistoria não encontrou o problema.', SYSDATE - 8);
    INSERT INTO atualizacao_relato (id, relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
      VALUES (9, 13, 5, 'ABERTO', 'EM_TRIAGEM', 'Confirmado por dois apoios.', SYSDATE - 58);
    INSERT INTO atualizacao_relato (id, relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
      VALUES (10, 13, 5, 'EM_TRIAGEM', 'ENCAMINHADO', 'Encaminhado à CET via SP156.', SYSDATE - 50);
    INSERT INTO atualizacao_relato (id, relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
      VALUES (11, 14, 6, 'ABERTO', 'EM_TRIAGEM', 'Baixa gravidade, mas fácil de resolver.', SYSDATE - 27);
    INSERT INTO atualizacao_relato (id, relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
      VALUES (12, 14, 6, 'EM_TRIAGEM', 'ENCAMINHADO', 'Encaminhado à subprefeitura.', SYSDATE - 25);
    INSERT INTO atualizacao_relato (id, relato_id, gestor_id, status_anterior, status_novo, comentario, criado_em)
      VALUES (13, 14, 6, 'ENCAMINHADO', 'RESOLVIDO', 'Calçada nivelada pela subprefeitura.', SYSDATE - 6);

    -- NOTIFICACAO (uma por mudança de status, para o autor e quem já apoiava)
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (1, 2, 2, 'O relato #2 que você registrou ou apoiou está em triagem pela rede de apoio.', 1, SYSDATE - 20);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (2, 4, 2, 'O relato #2 que você registrou ou apoiou está em triagem pela rede de apoio.', 1, SYSDATE - 20);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (3, 2, 3, 'O relato #3 que você registrou ou apoiou está em triagem pela rede de apoio.', 1, SYSDATE - 22);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (4, 2, 3, 'O relato #3 que você registrou ou apoiou foi encaminhado ao órgão responsável. Protocolo SP156-2026-0012345.', 1, SYSDATE - 18);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (5, 4, 5, 'O relato #5 que você registrou ou apoiou está em triagem pela rede de apoio.', 1, SYSDATE - 38);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (6, 1, 5, 'O relato #5 que você registrou ou apoiou está em triagem pela rede de apoio.', 1, SYSDATE - 38);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (7, 4, 5, 'O relato #5 que você registrou ou apoiou foi encaminhado ao órgão responsável. Protocolo SP156-2026-0013001.', 1, SYSDATE - 37);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (8, 1, 5, 'O relato #5 que você registrou ou apoiou foi encaminhado ao órgão responsável. Protocolo SP156-2026-0013001.', 1, SYSDATE - 37);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (9, 4, 5, 'O relato #5 que você registrou ou apoiou foi resolvido. Obrigado por ajudar a cidade!', 1, SYSDATE - 10);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (10, 1, 5, 'O relato #5 que você registrou ou apoiou foi resolvido. Obrigado por ajudar a cidade!', 1, SYSDATE - 10);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (11, 3, 7, 'O relato #7 que você registrou ou apoiou está em triagem pela rede de apoio.', 1, SYSDATE - 15);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (12, 4, 8, 'O relato #8 que você registrou ou apoiou foi descartado. Motivo: Entulho removido antes da vistoria.', 1, SYSDATE - 8);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (13, 1, 13, 'O relato #13 que você registrou ou apoiou está em triagem pela rede de apoio.', 1, SYSDATE - 58);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (14, 3, 13, 'O relato #13 que você registrou ou apoiou está em triagem pela rede de apoio.', 1, SYSDATE - 58);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (15, 4, 13, 'O relato #13 que você registrou ou apoiou está em triagem pela rede de apoio.', 1, SYSDATE - 58);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (16, 1, 13, 'O relato #13 que você registrou ou apoiou foi encaminhado ao órgão responsável. Protocolo SP156-2026-0009876.', 1, SYSDATE - 50);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (17, 3, 13, 'O relato #13 que você registrou ou apoiou foi encaminhado ao órgão responsável. Protocolo SP156-2026-0009876.', 1, SYSDATE - 50);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (18, 4, 13, 'O relato #13 que você registrou ou apoiou foi encaminhado ao órgão responsável. Protocolo SP156-2026-0009876.', 1, SYSDATE - 50);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (19, 4, 14, 'O relato #14 que você registrou ou apoiou está em triagem pela rede de apoio.', 1, SYSDATE - 27);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (20, 4, 14, 'O relato #14 que você registrou ou apoiou foi encaminhado ao órgão responsável. Protocolo SP156-2026-0014500.', 1, SYSDATE - 25);
    INSERT INTO notificacao (id, usuario_id, relato_id, mensagem, lida, criado_em)
      VALUES (21, 4, 14, 'O relato #14 que você registrou ou apoiou foi resolvido. Obrigado por ajudar a cidade!', 0, SYSDATE - 6);

    -- Prioridade de todos os relatos com os apoios já contados
    UPDATE relato SET prioridade = fn_calcular_prioridade(gravidade, total_apoios, criado_em, status);

    -- A carga inicial não é uma operação de negócio: limpa a auditoria gerada por ela
    DELETE FROM auditoria_relato;

    -- As colunas IDENTITY continuam depois dos IDs fixos do seed
    FOR t IN (SELECT column_value AS tabela FROM TABLE(sys.odcivarchar2list(
                'USUARIO', 'REGIAO', 'DESCRICAO', 'RELATO', 'APOIO_RELATO', 'ATUALIZACAO_RELATO', 'NOTIFICACAO'))) LOOP
      EXECUTE IMMEDIATE 'ALTER TABLE ' || t.tabela
                     || ' MODIFY (id GENERATED BY DEFAULT AS IDENTITY (START WITH LIMIT VALUE))';
    END LOOP;
  END IF;

  UPDATE usuario
     SET senha_hash = '$2a$10$xCHtq1/BEvjc2Bwk9iQ37O84OOuuVQPqIF/pYmbI0hxqFP3jXuOBi'
   WHERE senha_hash = '$2a$10$VeAiHashIlustrativoSomenteParaDadosDeTestexxxxxxxxxxx';
END;
/

-- Os ALTER TABLE (colunas da V3 e IDENTITY acima) invalidam funções, triggers e pacotes
-- que dependem das tabelas. O Oracle recompilaria no primeiro uso, mas assim a lista de
-- objetos inválidos fica vazia, como no 00_instalar.sql do repositório database.
BEGIN
  DBMS_UTILITY.COMPILE_SCHEMA(schema => USER, compile_all => FALSE);
END;
/
