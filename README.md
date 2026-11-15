<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/identidade/logo-horizontal-escuro.png">
    <img src="docs/identidade/logo-horizontal-claro.png" alt="SpinVault — Coleção de Mídias" width="460">
  </picture>
</p>

<p align="center">
  <b>SpinVault</b> — o catálogo para Windows da sua coleção física de <b>vinis, CDs e fitas cassete</b>.
</p>

<p align="center">
  <img alt="versão" src="https://img.shields.io/badge/vers%C3%A3o-1.1.0-F59E0B">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-8.0-512BD4">
  <img alt="plataforma" src="https://img.shields.io/badge/plataforma-Windows-0078D6">
</p>

## Sobre

**SpinVault** (*spin* de girar o disco, *vault* de cofre: o lugar onde sua coleção fica guardada) é um aplicativo WPF (C# / .NET 8) que organiza em um só lugar seus discos, com capas, datas de lançamento, valores pagos, lista de desejos e estatísticas. Os dados ficam no seu computador, em um banco SQLite local — sem conta e sem nuvem.

## Funcionalidades

- Importação de planilha Excel, de CSV e da coleção exportada do **Discogs** (nota, conservação e observações vêm junto).
- Visualização por mídia (vinil, CD, cassete) com busca e filtros.
- Notas pessoais em cada álbum: avaliação de 1 a 5 estrelas, estado de conservação do disco e da capa, onde comprou e observações.
- Gêneros e listas (como "Favoritos" e "Para ouvir"), com filtros por gênero, década e lista.
- Capas: download em lote, individual ou colando o link de uma imagem.
- Busca automática de artista e data de lançamento (MusicBrainz e Cover Art Archive, sem chave de API).
- Completar datas de lançamento em lote.
- Lista de desejos separada da coleção.
- Detecção de álbuns duplicados, mesmo em mídias diferentes.
- Dashboard interativo: clique em um ano, formato etc. para filtrar todo o painel.
- Página **Resumo**: quanto você gastou por ano, por mídia e por artista, ranking dos artistas com mais álbuns e "este mês há X anos" (álbuns que fazem aniversário de lançamento no mês).
- **Compartilhar**: salve um resumo da coleção como imagem (PNG, formato para redes sociais) ou como relatório em PDF.
- Exportação para Excel (.xlsx) e CSV, com todos os campos (dá para importar de volta).
- Backup e restauração completos (.zip com banco e capas).
- **Backup automático**: de tempos em tempos o app salva uma cópia da coleção na pasta que você escolher (por exemplo, no OneDrive) e mantém só os últimos backups.
- **Aviso de atualização**: o app consulta a Release do GitHub e avisa quando existe uma versão nova (você decide se baixa; nada é instalado sozinho).
- Temas escuro e claro.

## Instalação (para quem só quer usar)

1. Abra a página de [**Releases**](../../releases/latest) deste repositório.
2. Em *Assets*, baixe **`SpinVault-Setup-1.1.0.exe`**.
3. Execute o arquivo e siga o assistente (não precisa ser administrador).
4. Abra o **SpinVault** pelo menu Iniciar ou pelo atalho da área de trabalho.

> **Aviso do Windows:** como o instalador não tem assinatura digital paga, o SmartScreen pode mostrar "O Windows protegeu seu computador". Clique em **Mais informações → Executar assim mesmo**.

Prefere não instalar? Baixe também na Release o `SpinVault-portatil-1.1.0.zip`, extraia e execute o `SpinVault.exe`.

Não é preciso instalar o .NET: o aplicativo já vem com tudo que precisa.

Para desinstalar, use *Configurações → Aplicativos*. Sua coleção **não** é apagada (veja "Onde ficam os dados").

## Licença

Defina a licença antes de tornar o repositório público (por exemplo MIT). Sem licença, o código fica com todos os direitos reservados ao autor.

## Créditos

Dados de álbuns: [MusicBrainz](https://musicbrainz.org). Capas: [Cover Art Archive](https://coverartarchive.org).
