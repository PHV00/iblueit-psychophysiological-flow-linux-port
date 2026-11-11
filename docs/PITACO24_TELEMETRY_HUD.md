# PITACO 2.4 — MVP de telemetria no HUD da Plataforma

## Objetivo
Conectar a telemetria JSON do PITACO 2.4 ao HUD ja existente, sem segunda COM,
sem mudar a movimentacao respiratoria e sem alterar o DeepDDA.

## Fluxo
1. `PitacoConnectionManager` mantem COM exclusiva e distribui pressao numerica ao receptor legado.
2. Quando conectado ao 2.4, o manager tambem chama `Pitaco24Telemetry.AcceptLine`.
   Esse caminho nao depende da presenca do `SerialControllerPitaco24` na cena.
3. `Pitaco24Telemetry` interpreta apenas `type=sample` e `type=g1`, mantendo
   um snapshot em memoria com prazo de validade de 3 segundos.
4. O componente **ja existente** `OximeterMeasuresPlataform`, na cena Plataforma,
   mostra FC experimental, contato e resultado do Gate 01. Quando o oximetro
   independente esta ativo, seu HUD original tem prioridade.

## Interpretacao
- `bpm_valid=false`, sem contato ou dados expirados: **FC -- bpm**.
- `g1` ainda sem janela: **SQI G1: em analise**.
- `g1` recebido e contato presente: aprovado/reprovado.
- **SpO2: indisponivel** em todos os modos PITACO 2.4; nao existe valor
  calibrado para uso clinico nesta etapa.
- Com perda da conexao, o estado anterior e apagado.
- Nenhum valor experimental altera seguranca, limites, recompensas ou DeepDDA.

## Teste manual obrigatorio (Unity 2022.3.4f1)
1. Testar primeiro a cena MainMenu: o icone 2.4 deve ficar online na COM3,
   sem abrir segunda porta.
2. Selecionar paciente e entrar na Plataforma. Verificar se o HUD de medidas
   exibe `PITACO 2.4 (experimental)` e deixa de mostrar `aguardando sinal`
   quando os JSONs chegam. A pressao deve continuar controlando o Blue.
3. Cobrir/descobrir o sensor: verificar alteracao de `Contato` e bloquear FC
   invalida; aguardar uma janela G1 de aproximadamente 5s.
4. Retirar o USB, aguardar a reconexao automatica e confirmar que o
   HUD nao reutiliza medidas antigas.
5. Testar o oximetro independente (se disponivel): o HUD antigo deve ter
   prioridade. Testar PITACO 2.0 sem mudanca visual inesperada.
6. Testar navegacao entre cenas, minijogos e pause, verificando a ausencia
   de `NullReferenceException`, perda de controle e disputa pela COM.

## Avisos de Missing Script
Os logs `The referenced script ... is missing` em `Sprite`/`Sprite (1)`
aparecem durante `SpawnerTarget.SpawnTarget` e `SpawnerRelax.InstantiateRelaxObject`.
Sao uma pendencia separada: localizar o prefab de origem, conferir componentes
`Missing (Mono Script)` e restaurar script e seu arquivo `.meta`/GUID.
Nao remover componentes automaticamente: podem ser essenciais para colisao,
pontuacao e outros comportamentos. Investigar em uma branch separada.

## Limitacoes
Este PR contem implementacao e roteiro de teste; **nao executou a Unity,
nao comprova compilacao de cenas e nao valida BPM/SpO2 clinicamente**.
