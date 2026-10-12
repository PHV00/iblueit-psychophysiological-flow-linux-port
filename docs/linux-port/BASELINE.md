# I Blue It — Portabilidade Linux

## Baseline

- Upstream: `UDESC-LARVA/iblueit-psychophysiological-flow`
- Commit original: `ac7b75aa4437132ed3f4281165dc5cefe7b9cb6c`
- Desenvolvimento na branch `chore/linux-port-baseline`, sem alterações diretas à `main`.

## Invariantes

- Não alterar firmware, protocolo e unidades sem testes específicos.
- Não alterar as fórmulas de conversão Pa→fluxo, filtros, calibração, sinal ou limiares.
- Não alterar o comportamento do DeepDDA, seus modelos e parâmetros.
- O transporte Windows/Linux deve entregar as mesmas mensagens ao Unity.

## Escopo do porte

- Portabilidade de mensagens, serial e caminhos de arquivos.
- Remover dependências locais absolutas após fixar as versões originais.
- Testes de inicialização, sessões, calibração e equivalência de dados.

## Investigações independentes

- Contrato de unidades entre ESP32, Bridge e Unity (Pa vs L/min).
- Scripts ausentes de scenes/prefabs.
- Modo offline, persistência de pacientes, configuração de fases.

## Estado

Este documento registra apenas o plano/baseline, sem alegar que o jogo já está operacional no Linux.
