# RoadLife Telemetria — organização do código-fonte

## Estado atual (migração em andamento)

**Ainda não existe aqui uma cópia integral e compilável do aplicativo principal.** A base histórica do programa está dividida em `gat-telemetria-dotnet-1.0.28/source.part.00` e `source.bin.01` até `source.bin.05`. O workflow `.github/workflows/build-gat-telemetria-roadlife-cargo-1.0.68.12.yml` reconstrói essa base e aplica scripts de `gat-telemetria-allinone-1.0.29/`.

### Fontes existentes

- Aplicativo principal: `gat-telemetria-dotnet-1.0.28/` (arquivo-fonte compactado, não um projeto C# editável no GitHub).
- Correções históricas e instalador: `gat-telemetria-allinone-1.0.29/`.
- Dashboard RoadLife: `gat-dash/roadlife/` e `Projeto Dashboard2/`.
- Manifesto estável: `update/latest.json` (não modificar até validar pacote e SHA-256).
- Instaladores e binários: `releases/` e `releases-test/`.
- Automação Windows: `.github/workflows/build-gat-telemetria-roadlife-cargo-1.0.68.12.yml`.

## Estrutura alvo

```text
RoadLife-Telemetria/
  src/           # arquivos .cs e .csproj completos da base validada
  Dashboard/     # integração e assets do painel
  Assets/        # recursos distribuídos
  Installer/     # configuração do instalador
  Updater/       # atualização e manifesto
  README.md
```

## Plano seguro de migração

1. Reconstruir **uma única vez** o projeto histórico em ambiente Windows com todos os patches, identificando a última versão funcional.
2. Verificar compilação, telemetria, limites, multas, voz, dashboard e atualização.
3. Versionar os arquivos-fonte finais em `src/` (sem credenciais, bancos ou arquivos pessoais).
4. Alterar o workflow para compilar **diretamente de `src/`**, sem reconstruir ZIP nem aplicar patches em cadeia.
5. Publicar um pacote testado, gerar SHA-256 e só então atualizar o manifesto estável.
6. Após a validação, arquivar o fluxo legado; não apagá-lo antes da migração.

**Regra:** toda nova correção deve ser feita nos arquivos-fonte consolidados, revisada e compilada antes de publicação. Este diretório documenta a migração, mas não representa ainda um projeto compilável.
