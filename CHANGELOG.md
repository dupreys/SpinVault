# Histórico de versões

## 1.1.0
**Backup e atualizações**
- **Backup automático:** o SpinVault salva uma cópia da coleção (banco e capas) de tempos em tempos — todo dia, toda semana ou todo mês — e só quando algo mudou. Você escolhe a pasta (por exemplo, dentro do OneDrive) e quantos backups manter (3 a 30); os mais antigos são apagados sozinhos. Backups que você salvou manualmente nunca são apagados. Também há o botão **Fazer backup agora**.
- **Aviso de atualização:** uma vez por dia o app consulta a última Release no GitHub e, se houver versão nova, mostra um aviso no topo com o botão **Baixar** (ou **Agora não**). Nada é instalado sozinho. Pode ser desligado, e há o botão **Verificar agora**.
- Nova janela **Configurações** (menu lateral) reúne essas opções.

**Página Resumo** (menu lateral)
- **Valor da coleção:** valor investido, ticket médio e gasto por ano de compra, por mídia e por artista.
- **Rankings:** artistas com mais álbuns.
- **Este mês há X anos:** álbuns lançados no mês atual, com os anos de aniversário (os redondos ganham destaque e o dia de hoje é marcado). Datas 01/01 são tratadas como "só o ano" e não entram.
- **Compartilhar:** salve o resumo como imagem PNG (1080 × 1350, com capas e rankings) ou como relatório em PDF (A4, com texto selecionável).

**Notas, listas e importação**
- **Notas pessoais:** avaliação por estrelas (1 a 5), conservação do disco/fita/CD e da capa (escala M, NM, VG+, VG, G+, G, F, P), local de compra e observações.
- **Gêneros e listas:** campo de gênero com sugestões e listas livres por álbum (já vêm "Favoritos" e "Para ouvir"). Botão de coração para favoritar e filtros por gênero, década e lista.
- **Importação de CSV e Discogs:** reconhece sozinho o CSV exportado do Discogs, o CSV do próprio app e outros CSV com colunas parecidas (separador e acentuação detectados automaticamente).
- **Exportação completa:** Excel e CSV agora incluem os campos novos e podem ser importados de volta.

**Outros**
- O banco de dados é atualizado automaticamente; a coleção da versão 1.0 continua intacta.
- O editor de álbum passou a rolar quando a tela é pequena.

## 1.0.0
Primeira versão pública, com o nome **SpinVault**.

- Importação da coleção a partir de planilha Excel.
- Catálogo de CD, vinil e fita cassete com capas em disco/estojo, busca e filtros.
- Temas escuro e claro.
- Download de capas (em lote, individual ou por link) e busca de dados do álbum (MusicBrainz).
- Completar datas de lançamento em lote.
- Exportação para Excel (.xlsx) e CSV.
- Backup e restauração (.zip com banco e capas).
- Lista de desejos.
- Detecção de álbuns duplicados, mesmo em mídias diferentes.
- Dashboard interativo com filtros cruzados (ano, formato etc.).
- Efeitos ao passar o mouse em todo o aplicativo.
- Nova identidade visual: logotipo e ícone do aplicativo.
- Instalador (Inno Setup) e release automática pelo GitHub Actions.
