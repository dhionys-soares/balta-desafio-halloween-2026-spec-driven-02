# Tarefas

| ID | Tarefa | Origem | Depende de | Concluída quando |
|---|---|---|---|---|
| T-01 | Criar projeto e estrutura de pastas | Constituição | | Projeto compila |
| T-02 | Modelar a entidade Link e a migração | RF-01, RF-08 | T-01 | Migração aplica no banco |
| T-03 | Gerar código aleatório de 7 caracteres | RF-02, RN-05, D-03 | T-01 | Teste unitário cobre formato e alfabeto |
| T-04 | Validar URL de destino | RN-02, borda 2048 | T-01 | Teste cobre http, https e inválidos |
| T-05 | Criar link com código automático | RF-01 | T-02, T-03, T-04 | Teste de integração cria e retorna 201 |
| T-06 | Aceitar código próprio com unicidade | RF-03, RN-01, RN-06, D-04 | T-05 | Código duplicado retorna 409 |
| T-07 | Redirecionar por código | RF-04, RN-03 | T-05 | Teste de integração retorna 302 |
| T-08 | Incrementar contador de forma atômica | RF-05, RN-04, D-02 | T-07 | Teste concorrente não perde contagem |
| T-09 | Validar expiração em UTC e bloquear link expirado | RF-06, RN-03 | T-05 | Link expirado retorna erro e não conta acesso |
| T-10 | Endpoint de estatísticas com chave | RF-07, D-05 | T-05 | Chave errada retorna 401 |
| T-11 | Validar alfabeto do código e regenerar quando gerado automaticamente já estiver em uso | RN-05, RN-06, D-03 | T-03, T-06 | Código inválido é rejeitado e código em uso é regenerado |