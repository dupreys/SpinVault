# Histórico de versões

## 1.2.0
- **Idioma / Language:** o SpinVault agora também está em **English**. Em **Configurações → Idioma / Language** você escolhe *Português (Brasil)* ou *English*; o app reinicia sozinho para aplicar e lembra a escolha. Na primeira abertura ele segue o idioma do Windows.
  - Tudo é traduzido: menus, telas, botões, dicas, mensagens de erro e avisos, o relatório em PDF, a imagem de compartilhar, os graus de conservação e as listas padrão (Favoritos / Para ouvir viram Favorites / To listen).
  - Datas e valores seguem o idioma (25/12/2026 e R$ 1.234,56 em português; 12/25/2026 e R$ 1,234.56 em inglês). O campo de data e o de preço aceitam o formato do idioma escolhido.
  - Exportação, importação, backup e restauração funcionam entre idiomas: o CSV e o Excel exportados em um idioma podem ser importados no outro.
- **Resumo:** o cartão "Este mês há X anos" agora se chama **"Este mês, alguns álbuns fazem aniversário"**.
- **Lista completa:** quando há mais álbuns do que cabem no cartão, o botão **"Ver mais N álbuns…"** abre uma janela pequena com todos os aniversários do mês, com capa, artista, mídia, data de lançamento completa, anos e dia.
- O relatório em PDF acompanha o novo título.
- **Ordenar e agrupar a coleção:** o botão **Ordenar** escolhe a ordem (artista, título, lançamento mais antigo ou mais novo, maior valor, melhor nota, adicionados por último) e agrupa em seções com título por **gênero**, **década** ou **artista**. Funciona na galeria de capas e na lista, e o app lembra a sua escolha. Dentro de cada seção vale a ordem escolhida; "sem gênero" e "sem data" ficam por último.
- **Empréstimos:** o botão **Emprestar** nos detalhes do álbum registra para quem você emprestou e desde quando (com sugestões de quem já pegou discos). O cartão ganha a etiqueta "Emprestado", os detalhes mostram há quantos dias o disco está fora e o botão vira **Devolver**. Há o filtro **Emprestados** em Filtros, o contador no topo da coleção e a coluna "Emprestado para" na lista.
- Exportação e importação (Excel e CSV) incluem as colunas **Emprestado para** e **Data do empréstimo**.
- O banco de dados é atualizado sozinho para guardar os empréstimos; nada se perde.
- **Janela adaptável:** o app agora se ajusta a qualquer tamanho de janela (o mínimo caiu de 1000 para 720 px de largura).
  - Em janela estreita o menu lateral mostra só os ícones e as margens diminuem.
  - Coleção: os botões do topo descem para baixo do título, a barra de busca e filtros quebra em mais de uma linha e o painel de detalhes fica mais fino. A lista ganhou rolagem lateral quando falta espaço.
  - Dashboard e Resumo: corrigido o desalinhamento dos cartões de indicadores ao mudar o número de colunas.
- **Desempenho:** trocar o filtro (Todos, Vinil, CD, Cassete), buscar, ordenar ou salvar um álbum não congela mais a tela. Os cartões novos são criados aos poucos (o botão já muda na hora e as capas vão aparecendo), cliques rápidos cancelam o trabalho do filtro anterior, e álbuns que não mudaram não têm o cartão recriado ao recarregar a coleção. O resumo (quantidade e valor) aparece de imediato.
- O instalador gerado pelo GitHub passa a ser pré-compilado (ReadyToRun), o que acelera a abertura do app.

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
